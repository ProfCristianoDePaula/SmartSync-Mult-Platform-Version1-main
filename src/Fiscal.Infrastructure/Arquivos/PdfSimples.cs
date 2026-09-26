using System.Text;

namespace Fiscal.Infrastructure.Arquivos;

/// <summary>
/// PDF 1.4 mínimo (uma ou mais páginas A4, Helvetica) para DANFE/DANFCE/
/// DANFSE simplificados — sem dependência externa. Texto em WinAnsi;
/// acentos são transliterados. Layout completo oficial é refinamento futuro
/// (PENDENCIAS.md F0-27); os dados exibidos vêm do XML/snapshots oficiais.
/// </summary>
public static class PdfSimples
{
    public static byte[] Gerar(string titulo, IReadOnlyList<string> linhas)
    {
        var objetos = new List<byte[]>();
        // 1: catalog, 2: pages, 3: font; páginas a partir de 4.
        var paginas = Paginar(linhas, 60);
        var kids = new StringBuilder();

        var numero = 4;
        foreach (var pagina in paginas)
        {
            var conteudo = MontarConteudo(titulo, pagina);
            objetos.Add(Obj(numero, $"<< /Length {conteudo.Length} >>\nstream\n{conteudo}\nendstream"));
            var numConteudo = numero;
            numero++;
            objetos.Add(Obj(numero,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                $"/Contents {numConteudo} 0 R /Resources << /Font << /F1 3 0 R >> >> >>"));
            kids.Append($"{numero} 0 R ");
            numero++;
        }

        var todos = new List<byte[]>
        {
            Obj(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            Obj(2, $"<< /Type /Pages /Kids [{kids.ToString().Trim()}] /Count {paginas.Count} >>"),
            Obj(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"),
        };
        todos.AddRange(objetos);

        var pdf = new List<byte>();
        pdf.AddRange(Encoding.ASCII.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));
        var offsets = new List<long>();
        for (var i = 0; i < todos.Count; i++)
        {
            offsets.Add(pdf.Count);
            pdf.AddRange(todos[i]);
        }
        var xref = pdf.Count;
        var sb = new StringBuilder();
        sb.Append($"xref\n0 {todos.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var off in offsets)
            sb.Append($"{off:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {todos.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        pdf.AddRange(Encoding.ASCII.GetBytes(sb.ToString()));
        return [.. pdf];
    }

    private static List<List<string>> Paginar(IReadOnlyList<string> linhas, int porPagina)
    {
        var paginas = new List<List<string>>();
        for (var i = 0; i < linhas.Count; i += porPagina)
            paginas.Add(linhas.Skip(i).Take(porPagina).ToList());
        if (paginas.Count == 0) paginas.Add([]);
        return paginas;
    }

    private static string MontarConteudo(string titulo, List<string> linhas)
    {
        var sb = new StringBuilder("BT /F1 11 Tf 36 800 Td 14 TL ");
        sb.Append(Txt(titulo));
        sb.Append(" ' ");
        sb.Append(Txt(new string('-', 72)));
        sb.Append(" ' ");
        foreach (var linha in linhas)
        {
            sb.Append(Txt(linha));
            sb.Append(" ' ");
        }
        sb.Append("ET");
        return sb.ToString();
    }

    private static string Txt(string s)
    {
        var limpo = Transl(s);
        return "(" + limpo.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)") + ") Tj";
    }

    private static byte[] Obj(int numero, string corpo)
        => Encoding.ASCII.GetBytes($"{numero} 0 obj\n{corpo}\nendobj\n");

    private static string Transl(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c switch
            {
                'á' or 'à' or 'ã' or 'â' => "a",
                'é' or 'ê' => "e",
                'í' => "i",
                'ó' or 'õ' or 'ô' => "o",
                'ú' or 'ü' => "u",
                'ç' => "c",
                'Á' or 'À' or 'Ã' or 'Â' => "A",
                'É' or 'Ê' => "E",
                'Í' => "I",
                'Ó' or 'Õ' or 'Ô' => "O",
                'Ú' or 'Ü' => "U",
                'Ç' => "C",
                '—' or '–' => "-",
                < (char)32 or > (char)126 => "?",
                _ => c.ToString()
            });
        return sb.ToString();
    }
}
