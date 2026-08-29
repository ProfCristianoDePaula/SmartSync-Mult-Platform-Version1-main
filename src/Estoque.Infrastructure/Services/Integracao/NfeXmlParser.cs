using System.Xml.Linq;

namespace Estoque.Infrastructure.Services.Integracao;

/// <summary>
/// Parser minimalista de NF-e (layout 4.00) — extrai itens de produto
/// (cProd/xProd/qCom/vUnCom/uCom) do grupo &lt;det&gt;. Lote/validade são
/// metadados customizados do projeto (elementos &lt;lote&gt;/&lt;validade&gt;).
/// XML inválido lança exceção tratada pelo worker (import → Erro).
/// </summary>
public static class NfeXmlParser
{
    public sealed record NfeItem(
        string CProd,
        string? XProd,
        decimal QCom,
        decimal VUnCom,
        string? UCom,
        string Lote,
        DateOnly? Validade);

    public static List<NfeItem> Parse(byte[] content)
    {
        using var stream = new MemoryStream(content);
        var doc = XDocument.Load(stream);

        var root = doc.Root ?? throw new InvalidOperationException("XML vazio.");

        var items = new List<NfeItem>();

        foreach (var det in root.Descendants().Where(d => d.Name.LocalName == "det"))
        {
            var prod = det.Elements().FirstOrDefault(e => e.Name.LocalName == "prod");
            if (prod is null)
                continue;

            string? Get(string local) =>
                prod.Elements().FirstOrDefault(e => e.Name.LocalName == local)?.Value.Trim();

            var cProd = Get("cProd");
            if (string.IsNullOrWhiteSpace(cProd))
                continue;

            var qCom = decimal.TryParse(Get("qCom")?.Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0;
            var vUnCom = decimal.TryParse(Get("vUnCom")?.Replace(',', '.'),
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

            var lote = Get("lote") ?? string.Empty;
            DateOnly? validade = null;
            var validadeRaw = Get("validade");
            if (!string.IsNullOrWhiteSpace(validadeRaw))
                validade = DateOnly.TryParse(validadeRaw, out var vd) ? vd : null;

            items.Add(new NfeItem(cProd!, Get("xProd"), qCom, vUnCom, Get("uCom"), lote, validade));
        }

        return items;
    }
}
