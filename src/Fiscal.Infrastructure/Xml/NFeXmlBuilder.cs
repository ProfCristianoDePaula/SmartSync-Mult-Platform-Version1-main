using System.Globalization;
using System.Text;
using System.Xml;
using Fiscal.Application.Xml;
using Fiscal.Domain.Common;

namespace Fiscal.Infrastructure.Xml;

/// <summary>
/// Builder do XML (Fiscal-9, offline): subconjunto fiel do leiaute 4.00
/// (ide/emit/dest/det/imposto/total/transp/pag/infAdic + IBSCBS quando a
/// tabela de regras exige). Obrigatoriedade do IBS/CBS em tabela de dados
/// versionada (<see cref="IbsCbsRegras"/>), não em if espalhado.
/// Produto sem NCM é recusado antes de gerar.
/// </summary>
public sealed class NFeXmlBuilder : INFeXmlBuilder
{
    public const string NamespaceSimulador = "urn:smartsync:fiscal:simulador";

    public (string Xml, string Id) Construir(NFeBuildInput input)
    {
        if (input.Itens.Count == 0 || input.Itens.Count > 990)
            throw new BusinessRuleViolationException("Documento exige de 1 a 990 itens.");
        if (input.Modelo is not (55 or 65))
            throw new BusinessRuleViolationException("Modelo deve ser 55 ou 65.");

        var id = $"NFe{DateTime.UtcNow:yyyyMMddHHmmss}{input.Numero:D9}";
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("nfeSimulada", NamespaceSimulador);
            writer.WriteElementString("aviso", NamespaceSimulador, "SIMULAÇÃO — SEM VALOR FISCAL");
            writer.WriteStartElement("infNFe");
            writer.WriteAttributeString("Id", id);

            EscreverIde(writer, input);
            EscreverEmit(writer, input.Emitente);
            EscreverDest(writer, input.Destinatario);

            foreach (var item in input.Itens)
                EscreverDet(writer, item, input.Emitente.Crt);

            EscreverTotal(writer, input.Totais);
            writer.WriteStartElement("transp", NamespaceSimulador);
            writer.WriteElementString("modFrete", NamespaceSimulador, "9");
            writer.WriteEndElement();
            writer.WriteStartElement("pag", NamespaceSimulador);
            writer.WriteElementString("tPag", NamespaceSimulador, input.TPag);
            writer.WriteElementString("vPag", NamespaceSimulador, F2(input.Totais.ValorNota));
            writer.WriteEndElement();

            if (!string.IsNullOrWhiteSpace(input.NaturezaOperacao))
                writer.WriteElementString("infAdic", NamespaceSimulador, "NatOp: " + input.NaturezaOperacao.Trim());
            if (!string.IsNullOrWhiteSpace(input.QrCode))
                writer.WriteElementString("qrCode", NamespaceSimulador, input.QrCode.Trim());

            writer.WriteEndElement(); // infNFe
            writer.WriteEndElement(); // nfeSimulada
            writer.WriteEndDocument();
        }

        return (sb.ToString(), id);
    }

    private static void EscreverIde(XmlWriter w, NFeBuildInput input)
    {
        w.WriteStartElement("ide", NamespaceSimulador);
        w.WriteElementString("cUF", NamespaceSimulador, input.CUf.ToString("D2"));
        w.WriteElementString("mod", NamespaceSimulador, input.Modelo.ToString());
        w.WriteElementString("serie", NamespaceSimulador, input.Serie);
        w.WriteElementString("nNF", NamespaceSimulador, input.Numero.ToString());
        w.WriteElementString("dhEmi", NamespaceSimulador, input.DataEmissao.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        w.WriteElementString("tpAmb", NamespaceSimulador, input.TipoAmbiente.ToString());
        w.WriteEndElement();
    }

    private static void EscreverEmit(XmlWriter w, NFeEmitData e)
    {
        w.WriteStartElement("emit", NamespaceSimulador);
        w.WriteElementString("CNPJ", NamespaceSimulador, e.Cnpj);
        w.WriteElementString("xNome", NamespaceSimulador, e.Nome);
        if (!string.IsNullOrWhiteSpace(e.InscricaoEstadual))
            w.WriteElementString("IE", NamespaceSimulador, e.InscricaoEstadual);
        w.WriteElementString("CRT", NamespaceSimulador, e.Crt.ToString());
        w.WriteStartElement("enderEmit", NamespaceSimulador);
        w.WriteElementString("xLgr", NamespaceSimulador, e.Logradouro);
        w.WriteElementString("nro", NamespaceSimulador, e.Numero);
        w.WriteElementString("xBairro", NamespaceSimulador, e.Bairro);
        w.WriteElementString("cMun", NamespaceSimulador, e.CodigoMunicipio);
        w.WriteElementString("xMun", NamespaceSimulador, e.NomeMunicipio);
        w.WriteElementString("UF", NamespaceSimulador, e.Uf);
        w.WriteElementString("CEP", NamespaceSimulador, e.Cep);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void EscreverDest(XmlWriter w, NFeDestData d)
    {
        w.WriteStartElement("dest", NamespaceSimulador);
        if (!string.IsNullOrWhiteSpace(d.Cnpj)) w.WriteElementString("CNPJ", NamespaceSimulador, d.Cnpj);
        else if (!string.IsNullOrWhiteSpace(d.Cpf)) w.WriteElementString("CPF", NamespaceSimulador, d.Cpf);
        w.WriteElementString("xNome", NamespaceSimulador, d.Nome);
        if (d.CodigoMunicipio is not null)
        {
            w.WriteStartElement("enderDest", NamespaceSimulador);
            w.WriteElementString("xLgr", NamespaceSimulador, d.Logradouro ?? "");
            w.WriteElementString("nro", NamespaceSimulador, d.Numero ?? "");
            w.WriteElementString("xBairro", NamespaceSimulador, d.Bairro ?? "");
            w.WriteElementString("cMun", NamespaceSimulador, d.CodigoMunicipio);
            w.WriteElementString("xMun", NamespaceSimulador, d.NomeMunicipio ?? "");
            w.WriteElementString("UF", NamespaceSimulador, d.Uf ?? "");
            w.WriteElementString("CEP", NamespaceSimulador, d.Cep ?? "");
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    private static void EscreverDet(XmlWriter w, NFeItemData item, int crt)
    {
        if (string.IsNullOrWhiteSpace(item.Ncm))
            throw new BusinessRuleViolationException($"Item {item.Codigo}: NCM ausente — produto sem perfil fiscal.");

        w.WriteStartElement("det", NamespaceSimulador);
        w.WriteAttributeString("nItem", item.Numero.ToString());
        w.WriteStartElement("prod", NamespaceSimulador);
        w.WriteElementString("cProd", NamespaceSimulador, item.Codigo);
        w.WriteElementString("xProd", NamespaceSimulador, item.Descricao);
        w.WriteElementString("NCM", NamespaceSimulador, item.Ncm);
        w.WriteElementString("CFOP", NamespaceSimulador, item.Cfop);
        w.WriteElementString("uCom", NamespaceSimulador, item.UnidadeComercial);
        w.WriteElementString("qCom", NamespaceSimulador, F4(item.Quantidade));
        w.WriteElementString("vUnCom", NamespaceSimulador, F2(item.ValorUnitario));
        w.WriteElementString("vProd", NamespaceSimulador, F2(item.Quantidade * item.ValorUnitario));
        w.WriteEndElement();
        w.WriteStartElement("imposto", NamespaceSimulador);
        w.WriteStartElement("ICMS", NamespaceSimulador);
        var simples = crt is 1 or 2 or 4;
        if (simples)
            w.WriteElementString("CSOSN", NamespaceSimulador, item.Csosn ?? "");
        else
            w.WriteElementString("CST", NamespaceSimulador, item.CstIcms ?? "");
        w.WriteEndElement();
        if (IbsCbsRegras.Exigido(crt))
        {
            w.WriteStartElement("IBSCBS", NamespaceSimulador);
            w.WriteElementString("cClassTrib", NamespaceSimulador, item.CClassTrib ?? "");
            w.WriteElementString("CST", NamespaceSimulador, item.CstIbsCbs ?? "");
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void EscreverTotal(XmlWriter w, NFeTotaisData t)
    {
        w.WriteStartElement("total", NamespaceSimulador);
        w.WriteElementString("vNF", NamespaceSimulador, F2(t.ValorNota));
        w.WriteEndElement();
    }

    private static string F2(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string F4(decimal v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
}

