using System.Text.Json;
using Fiscal.Application.Nfse;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;

namespace Fiscal.Infrastructure.Nfse;

/// <summary>
/// Monta a DPS (identificação: município emissor, tipo/inscrição federal,
/// série, número + prestador + tomador + serviço + valores + IBSCBS).
/// Tamanhos do manual — confrontar (F0-28). Sem alíquota calculada (P4/R3).
/// </summary>
public sealed class DpsBuilder
{
    public sealed record TomadorDps(
        int TipoPessoa,
        string Documento,
        string Nome,
        string MunicipioIbge,
        string Cep);

    public sealed record ServicoDps(
        bool EhServico,
        string? ItemLc116,
        string? CodigoTribNacional,
        decimal? AliquotaIss,
        string? CClassTrib,
        string? CstIbsCbs);

    public string Montar(
        DocumentoFiscal doc,
        EmitenteFiscal emitente,
        TomadorDps tomador,
        ServicoDps servico,
        int numeroDps,
        string serieDps)
    {
        if (string.IsNullOrWhiteSpace(serieDps) || serieDps.Trim().Length > 5)
            throw new BusinessRuleViolationException("Série da DPS obrigatória (≤5).", "fiscal.dps.serie");
        if (numeroDps < 1)
            throw new BusinessRuleViolationException("Número da DPS inválido.", "fiscal.dps.numero");
        if (!servico.EhServico)
            throw new BusinessRuleViolationException("Perfil do item não é de serviço.", "fiscal.dps.servico");
        if (string.IsNullOrWhiteSpace(servico.ItemLc116))
            throw new BusinessRuleViolationException("Item da LC 116 ausente.", "fiscal.dps.lc116");

        using var itens = JsonDocument.Parse(doc.SnapshotItens);
        var primeiro = itens.RootElement.EnumerateArray().FirstOrDefault();
        var discriminacao = primeiro.ValueKind == JsonValueKind.Object
            && primeiro.TryGetProperty("descricao", out var d) ? d.GetString() ?? "" : "";
        var valor = primeiro.ValueKind == JsonValueKind.Object
            && primeiro.TryGetProperty("vu", out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDecimal() : 0;

        var dps = new Dictionary<string, object?>
        {
            ["ambiente"] = (int)doc.Ambiente,
            ["municipioEmissor"] = emitente.Endereco.CodigoIbgeMunicipio,
            ["tipoInscricaoFederal"] = emitente.Cnpj.Numero.Length == 14 ? 2 : 1,
            ["inscricaoFederal"] = emitente.Cnpj.Numero,
            ["serie"] = serieDps.Trim(),
            ["numero"] = numeroDps,
            ["prestador"] = new Dictionary<string, object?>
            {
                ["cnpj"] = emitente.Cnpj.Numero,
                ["razaoSocial"] = emitente.RazaoSocial,
                ["endereco"] = new Dictionary<string, object?>
                {
                    ["municipio"] = emitente.Endereco.CodigoIbgeMunicipio,
                    ["cep"] = emitente.Endereco.PostalCode
                }
            },
            ["tomador"] = new Dictionary<string, object?>
            {
                ["tipoPessoa"] = tomador.TipoPessoa,
                ["documento"] = tomador.Documento,
                ["nome"] = tomador.Nome,
                ["endereco"] = new Dictionary<string, object?>
                {
                    ["municipio"] = tomador.MunicipioIbge,
                    ["cep"] = tomador.Cep
                }
            },
            ["servico"] = new Dictionary<string, object?>
            {
                ["itemLc116"] = servico.ItemLc116,
                ["codigoTributacaoNacional"] = servico.CodigoTribNacional,
                ["discriminacao"] = discriminacao,
                ["valor"] = valor,
                ["aliquotaIss"] = servico.AliquotaIss,
                ["issRetido"] = false
            },
            ["valores"] = new Dictionary<string, object?> { ["valorServico"] = valor }
        };

        if (servico.CClassTrib is not null || servico.CstIbsCbs is not null)
            dps["ibsCbs"] = new Dictionary<string, object?>
            {
                ["cClassTrib"] = servico.CClassTrib,
                ["cst"] = servico.CstIbsCbs
            };

        return JsonSerializer.Serialize(new Dictionary<string, object?> { ["dps"] = dps });
    }
}
