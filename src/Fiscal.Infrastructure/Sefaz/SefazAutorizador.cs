using System.Text.Json;
using Fiscal.Application.Certificados;
using Fiscal.Application.Emissao;
using Fiscal.Application.Emitentes;
using Fiscal.Application.Repositories;
using Fiscal.Application.Xml;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Autorizador SEFAZ real (Fiscal-10, HOMOLOGAÇÃO por padrão): monta o XML a
/// partir do documento + cadastros, valida no XSD oficial, assina com o
/// certificado do emitente e transmite pelo endpoint do catálogo.
/// Produção existe mas só habilita com `Fiscal:ProducaoHabilitada=true`.
/// Nenhuma chamada real é feita pelo agente (só fixtures + HOMOLOGACAO.md).
/// </summary>
public sealed class SefazAutorizador(
    IEmitenteFiscalRepository emitentes,
    INFeAssembler assembler,
    INFeXmlBuilder builder,
    IXsdValidator xsd,
    IXmlSigner signer,
    ICertificadoResolver certificados,
    SefazSoapClient soap,
    FiscalDbContext db,
    IOptions<FiscalOptions> fiscalOptions,
    ILogger<SefazAutorizador> logger) : IAutorizadorFiscal
{
    public string Nome => "Sefaz";

    public async Task<TransmissaoResult> TransmitirAsync(DocumentoContexto ctx, CancellationToken ct)
    {
        var (doc, emitente) = await CarregarAsync(ctx.DocumentoId, ct);
        if (doc.Ambiente == AmbienteFiscal.Producao && !fiscalOptions.Value.ProducaoHabilitada)
            throw new BusinessRuleViolationException("Produção desabilitada (Fiscal:ProducaoHabilitada=false).");

        var input = assembler.Montar(doc, emitente);
        var (xml, id) = builder.Construir(input);
        xsd.Validar(xml, "oficial/4.00");

        using var cert = await certificados.ObterAsync(doc.TenantId,
            emitente.BranchId.Value == Guid.Empty ? null : emitente.BranchId, doc.Ambiente, ct);
        var assinado = signer.Assinar(xml, cert, id);
        if (!signer.Verificar(assinado))
            throw new BusinessRuleViolationException("Assinatura não verificada após assinar.");

        var endpoint = await ResolverEndpointAsync(emitente, doc.Ambiente, SefazServico.Autorizacao, ct);
        var lote = SoapEnvelopes.LoteEnvio(doc.Numero.ToString().PadLeft(15, '0'), assinado);
        var resposta = await soap.EnviarAsync(cert, endpoint.Url, "NFeAutorizacao",
            "http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao/nfeAutorizacaoLote", lote, ct);

        if (!resposta.HttpOk)
            return new TransmissaoResult(ResultadoTransmissao.ErroTecnico, null,
                "Falha HTTP na SEFAZ (sem resultado fiscal).", null);

        var (cStat, motivo, protocolo, recibo) = SefazSoapClient.Interpretar(resposta.Corpo);
        doc.GuardarXmlEnviado(assinado);
        await db.SaveChangesAsync(ct);

        // Lote assíncrono: recibo → consulta de recibo (polling no worker).
        if (!string.IsNullOrWhiteSpace(recibo) && CStatTabela.EfeitoDe(cStat) == ResultadoTransmissao.Aguardando)
            return new TransmissaoResult(ResultadoTransmissao.Aguardando, cStat, motivo, recibo);

        var efeito = CStatTabela.EfeitoDe(cStat);
        logger.LogInformation("SEFAZ {Ambiente}: cStat {CStat} ({Significado}).",
            doc.Ambiente, cStat, CStatTabela.SignificadoDe(cStat));
        return new TransmissaoResult(efeito, cStat, motivo, protocolo);
    }

    public async Task<TransmissaoResult> ConsultarAsync(DocumentoContexto ctx, CancellationToken ct)
    {
        var (doc, emitente) = await CarregarAsync(ctx.DocumentoId, ct);
        using var cert = await certificados.ObterAsync(doc.TenantId,
            emitente.BranchId.Value == Guid.Empty ? null : emitente.BranchId, doc.Ambiente, ct);

        var tpAmb = doc.Ambiente == AmbienteFiscal.Producao ? "1" : "2";
        var endpoint = await ResolverEndpointAsync(emitente, doc.Ambiente, SefazServico.ConsultaProtocolo, ct);
        var resposta = await soap.EnviarAsync(cert, endpoint.Url, "NfeConsultaProtocolo",
            "http://www.portalfiscal.inf.br/nfe/wsdl/NfeConsultaProtocolo/nfeConsultaNF",
            SoapEnvelopes.ConsultaProtocolo(tpAmb, ctx.Chave ?? ""), ct);

        if (!resposta.HttpOk)
            return new TransmissaoResult(ResultadoTransmissao.ErroTecnico, null, "Falha HTTP na consulta.", null);

        var (cStat, motivo, protocolo, _) = SefazSoapClient.Interpretar(resposta.Corpo);
        return new TransmissaoResult(CStatTabela.EfeitoDe(cStat), cStat, motivo, protocolo);
    }

    public async Task<TransmissaoResult> CancelarAsync(DocumentoContexto ctx, string justificativa, CancellationToken ct)
    {
        var (doc, emitente) = await CarregarAsync(ctx.DocumentoId, ct);
        var tpAmb = doc.Ambiente == AmbienteFiscal.Producao ? "1" : "2";
        var eventoXml =
            $"<evento xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"1.00\">" +
            $"<infEvento Id=\"ID110111{ctx.Chave}1\">" +
            $"<cOrgao>91</cOrgao><tpAmb>{tpAmb}</tpAmb>" +
            $"<CNPJ>{emitente.Cnpj.Numero}</CNPJ><chNFe>{ctx.Chave}</chNFe>" +
            $"<dhEvento>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}Z</dhEvento>" +
            $"<tpEvento>110111</tpEvento><nSeqEvento>1</nSeqEvento><verEvento>1.00</verEvento>" +
            $"<detEvento versao=\"1.00\"><descEvento>Cancelamento</descEvento>" +
            $"<xJust>{System.Security.SecurityElement.Escape(justificativa)}</xJust>" +
            $"</detEvento></infEvento></evento>";

        using var cert = await certificados.ObterAsync(doc.TenantId,
            emitente.BranchId.Value == Guid.Empty ? null : emitente.BranchId, doc.Ambiente, ct);
        var assinado = signer.Assinar(eventoXml, cert, $"ID110111{ctx.Chave}1");

        var endpoint = await ResolverEndpointAsync(emitente, doc.Ambiente, SefazServico.RecepcaoEvento, ct);
        var resposta = await soap.EnviarAsync(cert, endpoint.Url, "RecepcaoEvento",
            "http://www.portalfiscal.inf.br/nfe/wsdl/RecepcaoEvento/nfeRecepcaoEvento",
            SoapEnvelopes.RecepcaoEvento(assinado), ct);

        if (!resposta.HttpOk)
            return new TransmissaoResult(ResultadoTransmissao.ErroTecnico, null, "Falha HTTP no evento.", null);

        var (cStat, motivo, protocolo, _) = SefazSoapClient.Interpretar(resposta.Corpo);
        return new TransmissaoResult(CStatTabela.EfeitoDe(cStat), cStat, motivo, protocolo);
    }

    /// <summary>Polling de recibo (NfeRetAutorizacao).</summary>
    public async Task<TransmissaoResult> ConsultarReciboAsync(DocumentoContexto ctx, string recibo, CancellationToken ct)
    {
        var (doc, emitente) = await CarregarAsync(ctx.DocumentoId, ct);
        using var cert = await certificados.ObterAsync(doc.TenantId,
            emitente.BranchId.Value == Guid.Empty ? null : emitente.BranchId, doc.Ambiente, ct);

        var tpAmb = doc.Ambiente == AmbienteFiscal.Producao ? "1" : "2";
        var endpoint = await ResolverEndpointAsync(emitente, doc.Ambiente, SefazServico.RetAutorizacao, ct);
        var resposta = await soap.EnviarAsync(cert, endpoint.Url, "NfeRetAutorizacao",
            "http://www.portalfiscal.inf.br/nfe/wsdl/NfeRetAutorizacao/nfeRetAutorizacaoLote",
            SoapEnvelopes.ConsultaReciboTp(tpAmb, recibo), ct);

        if (!resposta.HttpOk)
            return new TransmissaoResult(ResultadoTransmissao.ErroTecnico, null, "Falha HTTP no recibo.", null);

        var (cStat, motivo, protocolo, _) = SefazSoapClient.Interpretar(resposta.Corpo);
        return new TransmissaoResult(CStatTabela.EfeitoDe(cStat), cStat, motivo, protocolo ?? recibo);
    }

    /// <summary>StatusServico em homologação (testar-conexão).</summary>
    public async Task<TransmissaoResult> StatusServicoAsync(
        TenantId tenantId, Guid emitenteId, CancellationToken ct)
    {
        var emitente = await emitentes.GetAsync(tenantId, emitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");
        using var cert = await certificados.ObterAsync(tenantId,
            emitente.BranchId.Value == Guid.Empty ? null : emitente.BranchId,
            AmbienteFiscal.Homologacao, ct);

        var uf = await db.UfsFiscais.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Sigla == emitente.Endereco.State, ct);
        var endpoint = await ResolverEndpointAsync(emitente, AmbienteFiscal.Homologacao, SefazServico.StatusServico, ct);
        var resposta = await soap.EnviarAsync(cert, endpoint.Url, "NfeStatusServico",
            "http://www.portalfiscal.inf.br/nfe/wsdl/NfeStatusServico/nfeStatusServicoNF",
            SoapEnvelopes.StatusServico().Replace("__TPAMB__", "2").Replace("__CUF__", (uf?.CodigoIbge ?? 0).ToString()), ct);

        if (!resposta.HttpOk)
            return new TransmissaoResult(ResultadoTransmissao.ErroTecnico, null, "Falha HTTP.", null);
        var (cStat, motivo, _, _) = SefazSoapClient.Interpretar(resposta.Corpo);
        return new TransmissaoResult(CStatTabela.EfeitoDe(cStat), cStat, motivo, null);
    }

    private async Task<(DocumentoFiscal Doc, EmitenteFiscal Emitente)> CarregarAsync(Guid documentoId, CancellationToken ct)
    {
        var doc = await db.DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == DocumentoFiscalId.From(documentoId), ct)
            ?? throw new BusinessRuleViolationException("Documento não encontrado.");
        var emitente = await emitentes.GetAsync(doc.TenantId, doc.EmitenteId.Value, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");
        return (doc, emitente);
    }

    private async Task<SefazEndpointLite> ResolverEndpointAsync(
        EmitenteFiscal emitente, AmbienteFiscal ambiente, SefazServico servico, CancellationToken ct)
    {
        var uf = await db.UfsFiscais.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Sigla == emitente.Endereco.State, ct)
            ?? throw new BusinessRuleViolationException($"UF {emitente.Endereco.State} fora do catálogo.");
        var autorizador = uf.AutorizadorNFe switch
        {
            AutorizadorTipo.Proprio => uf.Sigla,
            AutorizadorTipo.Svrs => "SVRS",
            AutorizadorTipo.Svan => "SVAN",
            _ => throw new BusinessRuleViolationException($"UF {uf.Sigla} sem autorizador definido.")
        };

        var todos = await db.SefazEndpoints.AsNoTracking()
            .Where(e => e.Autorizador == autorizador && e.Modelo == ModeloFiscal.NFe55
                && e.Servico == servico && e.Ambiente == ambiente)
            .OrderByDescending(e => e.VerificadoEm)
            .ToListAsync(ct);
        var ep = todos.FirstOrDefault(t => t.VersaoServico == "4.00") ?? todos.FirstOrDefault()
            ?? throw new BusinessRuleViolationException(
                $"Sem endpoint {servico} para {autorizador} no ambiente {ambiente} (F0-13).");
        return new SefazEndpointLite(ep.Url);
    }

    private sealed record SefazEndpointLite(string Url);
}
