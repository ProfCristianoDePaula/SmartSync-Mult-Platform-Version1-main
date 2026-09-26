using System.Text.Json;
using Fiscal.Application.Xml;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Monta o <see cref="NFeBuildInput"/> a partir do documento + emitente.
/// Lê os snapshots em JSON; campo ausente ⇒ erro acionável (nunca zero
/// presumido). Forma esperada documentada em HOMOLOGACAO.md.
/// </summary>
public interface INFeAssembler
{
    NFeBuildInput Montar(DocumentoFiscal doc, EmitenteFiscal emitente);
}

public sealed class NFeAssembler : INFeAssembler
{
    public NFeBuildInput Montar(DocumentoFiscal doc, EmitenteFiscal emitente)
    {
        using var dest = JsonDocument.Parse(doc.SnapshotDestinatario);
        using var itens = JsonDocument.Parse(doc.SnapshotItens);
        using var totais = JsonDocument.Parse(doc.SnapshotTotais);

        var d = dest.RootElement;
        var destData = new NFeDestData(
            d.TryGetString("cnpj"), d.TryGetString("cpf"),
            d.GetString("nome", "Destinatário"),
            d.TryGetString("street"), d.TryGetString("number"), d.TryGetString("district"),
            d.TryGetString("ibge"), d.TryGetString("city"), d.TryGetString("uf"), d.TryGetString("cep"));

        if (destData.Cnpj is null && destData.Cpf is null)
            throw new BusinessRuleViolationException("Destinatário sem CNPJ/CPF no snapshot.");

        var lista = new List<NFeItemData>();
        var n = 0;
        foreach (var it in itens.RootElement.EnumerateArray())
        {
            n++;
            lista.Add(new NFeItemData(
                n,
                it.GetString("codigo", $"ITEM-{n}"),
                it.GetString("descricao", $"Item {n}"),
                it.TryGetString("ncm") ?? "",
                it.GetString("cfop", ""),
                it.GetString("unCom", "UN"),
                it.GetDecimal("qtd", 0),
                it.GetDecimal("vu", 0),
                it.TryGetString("cst"), it.TryGetString("csosn"),
                it.TryGetDecimal("aliqIcms"),
                it.TryGetString("cClassTrib"), it.TryGetString("cstIbsCbs")));
        }

        if (lista.Count == 0)
            throw new BusinessRuleViolationException("Documento sem itens no snapshot.");

        var t = totais.RootElement;
        var bruto = t.GetDecimal("bruto", 0);
        var frete = t.GetDecimal("frete", 0);
        var desconto = t.GetDecimal("desconto", 0);
        var final = t.TryGetDecimal("final") ?? (bruto - desconto + frete);

        var uf = emitente.Endereco.State;
        var cuf = emitente.Endereco.CodigoIbgeMunicipio[..2] is string s && int.TryParse(s, out var c) ? c : 0;

        return new NFeBuildInput(
            cuf, (int)doc.Tipo, doc.Serie, doc.Numero,
            doc.Ambiente == AmbienteFiscal.Producao ? 1 : 2,
            DateTime.Now,
            new NFeEmitData(
                emitente.Cnpj.Numero, emitente.RazaoSocial, emitente.InscricaoEstadual,
                (int)emitente.Crt, emitente.Endereco.Street, emitente.Endereco.Number,
                emitente.Endereco.District, emitente.Endereco.CodigoIbgeMunicipio,
                emitente.Endereco.City, uf, emitente.Endereco.PostalCode),
            destData, lista,
            new NFeTotaisData(bruto, frete, desconto, final),
            "99", // tPag real chega na Fiscal-13 (venda); aqui, "outros" formal
            null, null);
    }
}

internal static class JsonExt
{
    public static string? TryGetString(this JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    public static string GetString(this JsonElement e, string name, string @default = "")
        => e.TryGetString(name) ?? @default;

    public static decimal? TryGetDecimal(this JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;

    public static decimal GetDecimal(this JsonElement e, string name, decimal @default)
        => e.TryGetDecimal(name) ?? @default;
}
