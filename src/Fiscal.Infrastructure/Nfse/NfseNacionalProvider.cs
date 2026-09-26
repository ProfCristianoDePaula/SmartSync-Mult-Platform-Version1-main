using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Fiscal.Application.Certificados;
using Fiscal.Application.Nfse;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Fiscal.Infrastructure.Nfse;

/// <summary>
/// Provedor NFS-e Padrão Nacional (Fiscal-12): DPS assinada + GZip + Base64
/// via mTLS ao Sefin do ambiente. Consulta por chave; cancelamento por
/// evento; substituição BLOQUEADA (só se o manual permitir — F0-29).
/// Resultado desconhecido ⇒ consultar antes de reenviar (R6).
/// O modo do município é verificado pelo chamador (pipeline); este provedor
/// só atende o nacional.
/// </summary>
/// <summary>Transporte HTTP do Sefin (seam para fixtures não tocarem rede).</summary>
public interface INfseHttpTransport
{
    Task<(bool HttpOk, string Corpo)> PostJsonAsync(
        string baseUrl, string path, string json, X509Certificate2? cert, CancellationToken ct);
    Task<(bool HttpOk, string Corpo)> GetAsync(
        string url, X509Certificate2? cert, CancellationToken ct);
}

public sealed class NfseHttpTransport : INfseHttpTransport
{
    public async Task<(bool HttpOk, string Corpo)> PostJsonAsync(
        string baseUrl, string path, string json, X509Certificate2? cert, CancellationToken ct)
    {
        using var client = CriarCliente(cert);
        using var http = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + path);
        http.Content = new StringContent(json, Encoding.UTF8, NfseNacionalContrato.ContentType);
        try
        {
            using var response = await client.SendAsync(http, ct);
            return (response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        catch (TimeoutException) { throw; }
    }

    public async Task<(bool HttpOk, string Corpo)> GetAsync(
        string url, X509Certificate2? cert, CancellationToken ct)
    {
        using var client = CriarCliente(cert);
        try
        {
            using var response = await client.GetAsync(url, ct);
            return (response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(ct));
        }
        catch (TimeoutException) { throw; }
    }

    private static HttpClient CriarCliente(X509Certificate2? cert)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(30) };
        if (cert is not null)
            handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(60) };
    }
}

public sealed class NfseNacionalProvider(
    FiscalDbContext db,
    IEmitenteFiscalRepository emitentes,
    ICertificadoResolver certificados,
    INfseParametrizacaoClient parametrizacao,
    INfseHttpTransport transporte,
    IMemoryCache cache,
    ILogger<NfseNacionalProvider> logger) : INfseProvider
{
    public string Nome => "NfseNacional";

    public bool Atende(string codigoIbgeMunicipio) => true;

    public async Task<ResultadoNfse> EmitirAsync(DpsRequest request, CancellationToken ct)
    {
        var emitente = await emitentes.GetAsync(request.TenantId, request.EmitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        var cfg = await db.NfseMunicipioConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CodigoIbge == emitente.Endereco.CodigoIbgeMunicipio, ct);
        if (cfg is null || cfg.Modo != ModoEmissaoNfse.NacionalEmissorPublico)
            throw new BusinessRuleViolationException(
                "Município sem emissão pelo Padrão Nacional (ver situação).",
                "MunicipioSemEmissaoNacional");

        var ambiente = request.Ambiente;
        var baseUrl = await BaseSefinAsync(ambiente, ct);

        // Confere parametrização quando acessível; divergência não calcula nada sozinha.
        await ConferirParametrizacaoAsync(baseUrl, emitente, request.CodigoServico, ct);

        using var cert = await certificados.ObterAsync(request.TenantId,
            emitente.BranchId.Value == Guid.Empty ? null : emitente.BranchId, ambiente, ct);

        var pacote = Convert.ToBase64String(Gzip(Encoding.UTF8.GetBytes(request.DpsXml)));

        var (httpOk, corpo) = await transporte.PostJsonAsync(baseUrl,
            NfseNacionalContrato.PathEmissao,
            JsonSerializer.Serialize(new { dps = pacote }), cert, ct);
        if (!httpOk)
            return new ResultadoNfse(false, null, null, null,
                "HTTP sem sucesso no Sefin (sem resultado fiscal).", null);

        var resultado = InterpretarEmissao(corpo);
        logger.LogInformation("Sefin {Ambiente}: emissão DPS {Sucesso}.", ambiente, resultado.Sucesso);
        return resultado;
    }

    public async Task<ResultadoNfse> ConsultarAsync(string identificador, CancellationToken ct)
    {
        var baseUrl = await BaseSefinAsync(AmbienteFiscal.Homologacao, ct);
        var (httpOk, corpo) = await transporte.GetAsync(
            baseUrl.TrimEnd('/') + NfseNacionalContrato.PathConsulta + $"?chave={Uri.EscapeDataString(identificador)}",
            null, ct);
        if (!httpOk)
            return new ResultadoNfse(false, null, null, null, "HTTP sem sucesso.", null);

        return InterpretarEmissao(corpo);
    }

    public async Task<ResultadoEventoNfse> CancelarAsync(CancelarNfseRequest request, CancellationToken ct)
    {
        var baseUrl = await BaseSefinAsync(AmbienteFiscal.Homologacao, ct);
        var (httpOk, corpo) = await transporte.PostJsonAsync(baseUrl,
            NfseNacionalContrato.PathEvento,
            JsonSerializer.Serialize(new { chave = request.ChaveAcesso, motivo = request.Motivo }),
            null, ct);
        if (!httpOk)
            return new ResultadoEventoNfse(false, "HTTP sem sucesso.", null);
        return InterpretarEvento(corpo);
    }

    private async Task ConferirParametrizacaoAsync(
        string baseUrl, EmitenteFiscal emitente, string? codigoServico, CancellationToken ct)
    {
        try
        {
            var parametros = await parametrizacao.ObterParametrosAsync(
                baseUrl, emitente.Endereco.CodigoIbgeMunicipio, codigoServico, ct);
            if (!parametros.Conhecido)
                logger.LogWarning("Parametrização municipal desconhecida para {Ibge}; emissão segue com perfil cadastrado.",
                    emitente.Endereco.CodigoIbgeMunicipio);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao conferir parametrização (segue com perfil cadastrado).");
        }
    }

    private async Task<string> BaseSefinAsync(AmbienteFiscal ambiente, CancellationToken ct)
    {
        var chave = $"nfse:sefin:{(int)ambiente}";
        if (cache.TryGetValue(chave, out string? url) && url is not null)
            return url;

        var amb = await db.NfseAmbientes.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CodigoIbge == "*"
                && a.Modo == ModoEmissaoNfse.NacionalEmissorPublico
                && a.Ambiente == ambiente, ct);

        url = amb?.BaseUrlSefin
            ?? (ambiente == AmbienteFiscal.Producao
                ? "https://sefin.nfse.gov.br"
                : "https://sefin.producaorestrita.nfse.gov.br");
        cache.Set(chave, url, TimeSpan.FromMinutes(30));
        return url;
    }

    internal static ResultadoNfse InterpretarEmissao(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var root = doc.RootElement;
            var nfse = root.TryGetProperty(NfseNacionalContrato.Nos.RaizNfse, out var n) ? n : root;

            if (nfse.TryGetProperty(NfseNacionalContrato.Nos.Chave, out var chave))
                return new ResultadoNfse(true, chave.GetString(),
                    nfse.TryGetProperty(NfseNacionalContrato.Nos.Numero, out var num) ? num.GetString() : null,
                    nfse.TryGetProperty(NfseNacionalContrato.Nos.CodigoVerificacao, out var cv) ? cv.GetString() : null,
                    null, null);

            if (root.TryGetProperty(NfseNacionalContrato.Nos.Motivos, out var motivos))
                return new ResultadoNfse(false, null, null, null, motivos.ToString(), null);

            return new ResultadoNfse(false, null, null, null, "Resposta sem NFS-e nem motivos.", null);
        }
        catch
        {
            return new ResultadoNfse(false, null, null, null, "Resposta ilegível do Sefin.", null);
        }
    }

    internal static ResultadoEventoNfse InterpretarEvento(string corpo)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.TryGetProperty("sucesso", out var ok) && ok.ValueKind == JsonValueKind.True)
                return new ResultadoEventoNfse(true, null,
                    doc.RootElement.TryGetProperty("protocolo", out var p) ? p.GetString() : null);
            return new ResultadoEventoNfse(false,
                doc.RootElement.TryGetProperty(NfseNacionalContrato.Nos.Motivos, out var m) ? m.ToString() : "Evento rejeitado.",
                null);
        }
        catch
        {
            return new ResultadoEventoNfse(false, "Resposta ilegível do Sefin.", null);
        }
    }

    private static byte[] Gzip(byte[] dados)
    {
        using var ms = new MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
            gz.Write(dados, 0, dados.Length);
        return ms.ToArray();
    }
}
