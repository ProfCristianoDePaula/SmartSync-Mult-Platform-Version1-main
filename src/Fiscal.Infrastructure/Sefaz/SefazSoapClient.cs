using System.Text;
using System.Xml.Linq;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Transporte SOAP 1.2 (Fiscal-10, homologação): monta envelope por serviço,
/// envia com mTLS do emitente e interpreta a resposta. SOAP Fault, timeout e
/// limite de resposta têm tratamento explícito. HTTP 200 não é autorização.
/// </summary>
public sealed class SefazSoapClient(ISefazConnectionFactory conexoes)
{
    public sealed record SoapResposta(bool HttpOk, string Corpo);

    public async Task<SoapResposta> EnviarAsync(
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificado,
        string url,
        string servicoWsdl,
        string acaoSoap,
        string dadosXml,
        CancellationToken ct)
    {
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("URL SEFAZ deve ser HTTPS (catálogo).", nameof(url));

        var client = conexoes.Create(certificado);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.TryAddWithoutValidation("SOAPAction", acaoSoap);
        request.Content = new StringContent(
            SoapEnvelopes.Montar(servicoWsdl, dadosXml), Encoding.UTF8, "application/soap+xml");

        string corpo;
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.Content.Headers.ContentLength > 10_000_000)
                throw new InvalidOperationException("Resposta excede 10 MB.");
            corpo = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return new SoapResposta(false, corpo);
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (HttpRequestException ex) when (ex.InnerException is TimeoutException)
        {
            throw new TimeoutException("Timeout na SEFAZ.", ex);
        }

        return new SoapResposta(true, corpo);
    }

    /// <summary>Extrai cStat/xMotivo/protocolo/recibo do retorno (por documento quando houver).</summary>
    public static (string? CStat, string? Motivo, string? Protocolo, string? Recibo) Interpretar(string corpo)
    {
        try
        {
            var doc = XDocument.Parse(corpo, LoadOptions.None);
            string? Get(string local) => doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == local)?.Value.Trim();

            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault is not null)
                return (null, "SOAP Fault: " + fault.Value.Trim()[..Math.Min(200, fault.Value.Trim().Length)], null, null);

            var prot = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "protNFe")
                ?? doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "infProt");
            var protocolo = prot?.Descendants().FirstOrDefault(e => e.Name.LocalName == "nProt")?.Value.Trim()
                ?? Get("nProt");

            return (Get("cStat"), Get("xMotivo"), protocolo, Get("nRec"));
        }
        catch
        {
            return (null, "Resposta ilegível do autorizador.", null, null);
        }
    }
}
