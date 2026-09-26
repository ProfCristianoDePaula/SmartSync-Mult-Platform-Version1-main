using System.Security;
using System.Text;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Envelopes SOAP 1.2 por serviço do leiaute 4.00 (estrutura conforme o
/// padrão dos WSDLs do Portal Nacional — CONFRONTAR contra o WSDL de cada
/// serviço antes de homologação real, PENDENCIAS.md F0-24; nunca reutilizar
/// envelopes de versões antigas sem conferir).
/// </summary>
public static class SoapEnvelopes
{
    private const string NsSoap = "http://www.w3.org/2003/05/soap-envelope";
    private const string NsNFe = "http://www.portalfiscal.inf.br/nfe/wsdl/";

    public static string Montar(string servicoWsdl, string dadosXml, string versaoDados = "4.00")
    {
        var sb = new StringBuilder();
        sb.Append($"<soap12:Envelope xmlns:soap12=\"{NsSoap}\">");
        sb.Append("<soap12:Body>");
        sb.Append($"<nfeDadosMsg xmlns=\"{NsNFe}{servicoWsdl}\">");
        sb.Append(dadosXml);
        sb.Append("</nfeDadosMsg>");
        sb.Append("</soap12:Body>");
        sb.Append("</soap12:Envelope>");
        return sb.ToString();
    }

    public static string StatusServico(string versao = "4.00")
        => $"<consStatServ xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\"><tpAmb>__TPAMB__</tpAmb><cUF>__CUF__</cUF><xServ>STATUS</xServ></consStatServ>";

    public static string LoteEnvio(string loteId, string nfeXmlAssinado, string versao = "4.00")
        => $"<enviNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\"><idLote>{SecurityElement.Escape(loteId)}</idLote><indSinc>1</indSinc><NFe>{ExtrairInfNFe(nfeXmlAssinado)}</NFe></enviNFe>";

    public static string ConsultaReciboTp(string tpAmb, string numeroRecibo, string versao = "4.00")
        => $"<consReciNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\"><tpAmb>{tpAmb}</tpAmb><nRec>{SecurityElement.Escape(numeroRecibo)}</nRec></consReciNFe>";

    public static string ConsultaRecibo(string numeroRecibo, string tpAmb, string versao = "4.00")
        => $"<consReciNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\"><tpAmb>{tpAmb}</tpAmb><nRec>{SecurityElement.Escape(numeroRecibo)}</nRec></consReciNFe>";

    public static string ConsultaProtocolo(string tpAmb, string chave, string versao = "4.00")
        => $"<consSitNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\"><tpAmb>{tpAmb}</tpAmb><xServ>CONSULTAR</xServ><chNFe>{SecurityElement.Escape(chave)}</chNFe></consSitNFe>";

    public static string RecepcaoEvento(string eventoXmlAssinado)
        => $"<envEvento xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"4.00\">{ExtrairEvento(eventoXmlAssinado)}</envEvento>";

    public static string Inutilizacao(string tpAmb, string cUF, string cnpj, string mod, string serie,
        string nIni, string nFin, string xJust, string versao = "4.00")
        => $"<inutNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\"><infInut Id=\"ID{S(cUF)}{S(DateTime.UtcNow.Year.ToString()[2..])}{S(cnpj)}{S(mod)}{S(serie)}{S(nIni)}{S(nFin)}\"><tpAmb>{tpAmb}</tpAmb><xServ>INUTILIZAR</xServ><cUF>{S(cUF)}</cUF><ano>{DateTime.UtcNow.Year.ToString()[2..]}</ano><CNPJ>{S(cnpj)}</CNPJ><mod>{S(mod)}</mod><serie>{S(serie)}</serie><nNFIni>{S(nIni)}</nNFIni><nNFFin>{S(nFin)}</nNFFin><xJust>{SecurityElement.Escape(xJust)}</xJust></infInut></inutNFe>";

    private static string S(string v) => SecurityElement.Escape(v.Trim());

    private static string ExtrairInfNFe(string nfeXmlAssinado)
    {
        // Extrai o elemento infNFe assinado (não o envelope do simulador).
        var ini = nfeXmlAssinado.IndexOf("<infNFe", StringComparison.Ordinal);
        var fim = nfeXmlAssinado.IndexOf("</infNFe>", StringComparison.Ordinal);
        if (ini < 0 || fim < 0)
            throw new InvalidOperationException("XML sem elemento infNFe.");
        return nfeXmlAssinado.Substring(ini, fim - ini + "</infNFe>".Length);
    }

    private static string ExtrairEvento(string eventoXml)
    {
        var ini = eventoXml.IndexOf("<evento", StringComparison.Ordinal);
        var fim = eventoXml.IndexOf("</evento>", StringComparison.Ordinal);
        if (ini < 0 || fim < 0)
            throw new InvalidOperationException("XML sem elemento evento.");
        return eventoXml.Substring(ini, fim - "</evento>".Length + "</evento>".Length);
    }
}
