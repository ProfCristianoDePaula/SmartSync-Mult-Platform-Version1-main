using Fiscal.Application.Cadastros;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Infrastructure.Cadastros;

/// <summary>
/// Validação FISCAL FORMAL (P4): presença + formato + coerência CRT×(CST/CSOSN).
/// Alíquotas e classificações são INFORMADAS, nunca calculadas aqui.
/// </summary>
public sealed class ValidadorFiscal(IProdutoFiscalRepository produtos) : IValidadorFiscal
{
    public async Task<IReadOnlyList<string>> ValidarProdutoAsync(
        TenantId tenantId, Guid produtoId, CrtFiscal? crt, TipoDocumentoFiscal doc,
        CancellationToken ct = default)
    {
        var pendencias = new List<string>();
        var pf = await produtos.GetAsync(tenantId, produtoId, ct);

        if (pf is null)
        {
            pendencias.Add("Sem perfil fiscal cadastrado.");
            return pendencias;
        }

        if (doc == TipoDocumentoFiscal.NFSe)
        {
            if (pf.Tipo != TipoItemFiscal.Servico)
                pendencias.Add("Item não marcado como serviço.");
            if (string.IsNullOrWhiteSpace(pf.ItemLc116))
                pendencias.Add("Item da LC 116 ausente.");
            if (pf.AliquotaIss is null)
                pendencias.Add("Alíquota do ISS ausente.");
            return pendencias;
        }

        // NF-e/NFC-e: mercadoria.
        if (pf.Tipo != TipoItemFiscal.Mercadoria)
            pendencias.Add("Item não marcado como mercadoria.");
        if (string.IsNullOrWhiteSpace(pf.Ncm))
            pendencias.Add("NCM ausente.");
        if (string.IsNullOrWhiteSpace(pf.CfopDentroUf) || string.IsNullOrWhiteSpace(pf.CfopForaUf))
            pendencias.Add("CFOP dentro/fora da UF ausente.");
        if (string.IsNullOrWhiteSpace(pf.UnidadeComercial))
            pendencias.Add("Unidade comercial ausente.");

        if (crt is not null)
        {
            var simples = crt is CrtFiscal.SimplesNacional or CrtFiscal.SimplesExcesso or CrtFiscal.Mei;
            if (simples && string.IsNullOrWhiteSpace(pf.Csosn))
                pendencias.Add($"CSOSN ausente (CRT {crt} exige CSOSN, não CST).");
            if (!simples && string.IsNullOrWhiteSpace(pf.CstIcms))
                pendencias.Add($"CST do ICMS ausente (CRT {crt} exige CST, não CSOSN).");
        }

        return pendencias;
    }
}
