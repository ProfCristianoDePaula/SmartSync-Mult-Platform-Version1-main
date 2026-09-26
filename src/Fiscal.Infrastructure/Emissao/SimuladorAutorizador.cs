using System.Security.Cryptography;
using System.Text;
using Fiscal.Application.Emissao;

namespace Fiscal.Infrastructure.Emissao;

/// <summary>
/// Autorizador SIMULADO e determinístico (Fiscal-8): gatilhos no texto do
/// pedido — contém "REJEITAR" ⇒ rejeição; "TIMEOUT" ⇒ timeout (exceção);
/// "LENTO" ⇒ demora 2 s e autoriza. Toda saída carrega
/// "SIMULAÇÃO — SEM VALOR FISCAL" (R11). Nunca toca rede.
/// </summary>
public sealed class SimuladorAutorizador : IAutorizadorFiscal
{
    public string Nome => "Simulador";

    public async Task<TransmissaoResult> TransmitirAsync(DocumentoContexto ctx, CancellationToken ct)
    {
        var texto = ctx.SnapshotTotais ?? "";

        if (texto.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase))
            throw new TimeoutException("Timeout simulado após envio (SIMULAÇÃO — SEM VALOR FISCAL).");

        if (texto.Contains("LENTO", StringComparison.OrdinalIgnoreCase))
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

        if (texto.Contains("REJEITAR", StringComparison.OrdinalIgnoreCase))
            return new TransmissaoResult(ResultadoTransmissao.Rejeitado, "999",
                "Rejeição simulada (SIMULAÇÃO — SEM VALOR FISCAL).", null);

        return new TransmissaoResult(ResultadoTransmissao.Autorizado, "100",
            "Autorizado (SIMULAÇÃO — SEM VALOR FISCAL).", ProtocoloPara(ctx));
    }

    public Task<TransmissaoResult> ConsultarAsync(DocumentoContexto ctx, CancellationToken ct)
    {
        // Conciliação: se houve transmissão (mesmo com timeout), o autorizador
        // simulado a encontra — como na SEFAZ real após recibo.
        if ((ctx.SnapshotTotais ?? "").Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)
            || ctx.Chave is not null)
            return Task.FromResult(new TransmissaoResult(ResultadoTransmissao.Autorizado, "100",
                "Autorizado em conciliação (SIMULAÇÃO — SEM VALOR FISCAL).", ProtocoloPara(ctx)));

        return Task.FromResult(new TransmissaoResult(ResultadoTransmissao.Desconhecido, null,
            "Sem registro (SIMULAÇÃO — SEM VALOR FISCAL).", null));
    }

    public Task<TransmissaoResult> CancelarAsync(DocumentoContexto ctx, string justificativa, CancellationToken ct)
    {
        if ((ctx.SnapshotTotais ?? "").Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase)
            && !ctx.SnapshotTotais.Contains("CANCELA-TIMEOUT-OK", StringComparison.OrdinalIgnoreCase))
            throw new TimeoutException("Timeout simulado no cancelamento (SIMULAÇÃO — SEM VALOR FISCAL).");

        return Task.FromResult(new TransmissaoResult(ResultadoTransmissao.Autorizado, "135",
            "Cancelamento homologado (SIMULAÇÃO — SEM VALOR FISCAL).", ProtocoloPara(ctx) + "-CANC"));
    }

    internal static string ProtocoloPara(DocumentoContexto ctx)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ctx.DocumentoId.ToString()));
        return "SIMULACAO-" + Convert.ToHexString(hash)[..12];
    }

    /// <summary>XML didático do simulador (Fiscal-9 gera o leiaute real).</summary>
    internal static string XmlSimulado(DocumentoContexto ctx)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<nfeSimulada xmlns=\"urn:smartsync:fiscal:simulador\">");
        sb.Append("<aviso>SIMULAÇÃO — SEM VALOR FISCAL</aviso>");
        sb.Append($"<tipo>{(int)ctx.Tipo}</tipo><serie>{System.Security.SecurityElement.Escape(ctx.Serie)}</serie>");
        sb.Append($"<numero>{ctx.Numero}</numero>");
        sb.Append($"<chave>{System.Security.SecurityElement.Escape(ctx.Chave ?? "")}</chave>");
        sb.Append("</nfeSimulada>");
        return sb.ToString();
    }
}
