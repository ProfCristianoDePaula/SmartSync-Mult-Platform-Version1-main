using System.Text.Json;
using Fiscal.Application.Documentos;
using Fiscal.Application.Emissao;
using Fiscal.Application.Nfse;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Fiscal.Infrastructure.Nfse.DpsBuilder;

namespace Fiscal.Infrastructure.Nfse;

/// <summary>
/// Adapta NFSe ao pipeline (IAutorizadorFiscal): monta a DPS a partir do
/// documento + perfis (quando o snapshot traz clienteId/produtoId) ou dos
/// campos inline do snapshot, numera a DPS e transmite pelo provedor nacional.
/// Município fora do nacional ⇒ 422 MunicipioSemEmissaoNacional.
/// </summary>
public sealed class NfsePipelineAdapter(
    FiscalDbContext db,
    IEmitenteFiscalRepository emitentes,
    IClienteFiscalRepository clientes,
    IProdutoFiscalRepository produtos,
    ISerieNumeracaoService numeracao,
    DpsBuilder dpsBuilder,
    INfseProvider provedor,
    ILogger<NfsePipelineAdapter> logger) : IAutorizadorFiscal
{
    public string Nome => "NfsePipeline";

    public async Task<TransmissaoResult> TransmitirAsync(DocumentoContexto ctx, CancellationToken ct)
    {
        var (doc, emitente, tomador, servico, numeroDps, serieDps) = await MontarAsync(ctx.DocumentoId, ct);

        var dpsXml = dpsBuilder.Montar(doc, emitente, tomador, servico, numeroDps, serieDps);
        var codigoServico = servico.CodigoTribNacional;

        var resultado = await provedor.EmitirAsync(new DpsRequest(
            doc.TenantId, emitente.Id.Value, doc.Ambiente, doc.Id.Value,
            emitente.Endereco.CodigoIbgeMunicipio, serieDps, numeroDps, dpsXml, codigoServico), ct);

        doc.GuardarXmlEnviado(dpsXml);
        await db.SaveChangesAsync(ct);

        if (!resultado.Sucesso)
            return new TransmissaoResult(ResultadoTransmissao.Rejeitado, null, resultado.Motivo, null);

        logger.LogInformation("NFS-e emitida: chave {Chave}.", resultado.ChaveAcesso);
        return new TransmissaoResult(ResultadoTransmissao.Autorizado, "100",
            "NFS-e autorizada.", resultado.ChaveAcesso ?? resultado.NumeroNfse);
    }

    public async Task<TransmissaoResult> ConsultarAsync(DocumentoContexto ctx, CancellationToken ct)
    {
        var chave = ctx.Chave;
        if (string.IsNullOrWhiteSpace(chave))
            return new TransmissaoResult(ResultadoTransmissao.Desconhecido, null, "Sem chave para consulta.", null);

        var resultado = await provedor.ConsultarAsync(chave, ct);
        if (!resultado.Sucesso)
            return new TransmissaoResult(ResultadoTransmissao.Desconhecido, null, resultado.Motivo, null);

        return new TransmissaoResult(ResultadoTransmissao.Autorizado, "100",
            "NFS-e localizada.", resultado.ChaveAcesso ?? resultado.NumeroNfse);
    }

    public async Task<TransmissaoResult> CancelarAsync(DocumentoContexto ctx, string justificativa, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.Chave))
            return new TransmissaoResult(ResultadoTransmissao.ErroTecnico, null, "Sem chave para cancelar.", null);

        var resultado = await provedor.CancelarAsync(
            new CancelarNfseRequest(ctx.DocumentoId, ctx.Chave, justificativa), ct);
        if (!resultado.Sucesso)
            return new TransmissaoResult(ResultadoTransmissao.Rejeitado, null, resultado.Motivo, null);

        return new TransmissaoResult(ResultadoTransmissao.Autorizado, "101",
            "Cancelamento homologado.", resultado.Protocolo);
    }

    private async Task<(DocumentoFiscal Doc, EmitenteFiscal Emitente, TomadorDps Tomador, ServicoDps Servico, int NumeroDps, string SerieDps)>
        MontarAsync(Guid documentoId, CancellationToken ct)
    {
        var doc = await db.DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == DocumentoFiscalId.From(documentoId), ct)
            ?? throw new BusinessRuleViolationException("Documento não encontrado.");
        var emitente = await emitentes.GetAsync(doc.TenantId, doc.EmitenteId.Value, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        using var dest = JsonDocument.Parse(doc.SnapshotDestinatario);
        using var itens = JsonDocument.Parse(doc.SnapshotItens);
        var lista = itens.RootElement.EnumerateArray().ToList();
        if (lista.Count != 1)
            throw new BusinessRuleViolationException(
                "NFS-e com múltiplos serviços não suportada (um DPS por serviço).",
                "fiscal.nfse.multiplos-servicos");

        var d = dest.RootElement;
        TomadorDps tomador;
        if (d.TryGetProperty("clienteId", out var cidProp)
            && Guid.TryParse(cidProp.GetString(), out var clienteId))
        {
            var perfil = await clientes.GetAsync(doc.TenantId, clienteId, ct)
                ?? throw new BusinessRuleViolationException($"Cliente fiscal {clienteId} inexistente.");
            tomador = new TomadorDps(
                perfil.TipoPessoa == TipoPessoaFiscal.Fisica ? 1 : 2,
                perfil.Documento, perfil.Nome,
                perfil.Endereco.CodigoIbgeMunicipio, perfil.Endereco.PostalCode);
        }
        else
        {
            tomador = new TomadorDps(
                d.TryGetProperty("tipoPessoa", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : 1,
                d.TryGetProperty("documento", out var dc) ? dc.GetString() ?? "" : "",
                d.TryGetProperty("nome", out var nm) ? nm.GetString() ?? "" : "",
                d.TryGetProperty("ibge", out var ib) ? ib.GetString() ?? "" : "",
                d.TryGetProperty("cep", out var cp) ? cp.GetString() ?? "" : "");
            if (string.IsNullOrWhiteSpace(tomador.Documento) || string.IsNullOrWhiteSpace(tomador.Nome))
                throw new BusinessRuleViolationException("Tomador sem documento/nome no snapshot.");
        }

        var it = lista[0];
        ServicoDps servico;
        if (it.TryGetProperty("produtoId", out var pidProp)
            && Guid.TryParse(pidProp.GetString(), out var produtoId))
        {
            var perfil = await produtos.GetAsync(doc.TenantId, produtoId, ct)
                ?? throw new BusinessRuleViolationException($"Produto fiscal {produtoId} inexistente.");
            servico = new ServicoDps(perfil.Tipo == TipoItemFiscal.Servico,
                perfil.ItemLc116, perfil.CodigoTribNacional, perfil.AliquotaIss,
                perfil.CClassTrib, perfil.CstIbsCbs);
        }
        else
        {
            servico = new ServicoDps(true,
                it.TryGetProperty("itemLc116", out var lc) ? lc.GetString() : null,
                it.TryGetProperty("codTrib", out var ctrib) ? ctrib.GetString() : null,
                it.TryGetProperty("aliqIss", out var ai) && ai.ValueKind == JsonValueKind.Number ? ai.GetDecimal() : null,
                it.TryGetProperty("cClassTrib", out var cc) ? cc.GetString() : null,
                it.TryGetProperty("cstIbsCbs", out var cs) ? cs.GetString() : null);
        }

        var serieDps = "1";
        var numeroDps = await numeracao.ReservarAsync(emitente.Id, 200, serieDps, doc.Ambiente, ct);

        return (doc, emitente, tomador, servico, numeroDps, serieDps);
    }
}
