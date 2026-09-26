using Fiscal.Domain.Common;

namespace Fiscal.Domain.ValueObjects;

/// <summary>
/// Chave de acesso de 44 posições (NF-e/NFC-e): cUF(2) + AAMM(4) + CNPJ(14) +
/// modelo(2) + série(3) + número(9) + tipo de emissão(1) + código numérico(8) +
/// DV módulo 11. Composição descrita no material-base §5.2; vetores oficiais de
/// exemplo ainda pendentes (PENDENCIAS.md F0-21). CNPJ alfanumérico: sem regra
/// confirmada de chave — recusado aqui (F0-01).
/// </summary>
public sealed class ChaveAcesso : ValueObject
{
    public string Numero { get; }

    private ChaveAcesso(string numero) => Numero = numero;

    public static ChaveAcesso Montar(
        int cUF, int ano, int mes, string cnpj, int modelo,
        int serie, int numero, int tipoEmissao, int codigoNumerico)
    {
        if (cUF is < 11 or > 53) throw new ArgumentException("cUF inválido.", nameof(cUF));
        if (ano is < 2000 or > 2100 || mes is < 1 or > 12) throw new ArgumentException("Período inválido.");
        if ((cnpj ?? "").Any(char.IsLetter))
            throw new BusinessRuleViolationException(
                "CNPJ alfanumérico sem regra de chave confirmada (F0-01).", "fiscal.chave.cnpj-alfa");
        var digits = new string((cnpj ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 14)
            throw new ArgumentException("Chave exige CNPJ numérico de 14 dígitos.", nameof(cnpj));
        if (modelo is not (55 or 65)) throw new ArgumentException("Modelo deve ser 55 ou 65.", nameof(modelo));
        if (serie is < 0 or > 999) throw new ArgumentException("Série 0–999.", nameof(serie));
        if (numero is < 1 or > 999999999) throw new ArgumentException("Número 1–999999999.", nameof(numero));
        if (tipoEmissao is < 1 or > 9) throw new ArgumentException("Tipo de emissão 1–9.", nameof(tipoEmissao));
        if (codigoNumerico is < 0 or > 99999999) throw new ArgumentException("Código numérico 0–99999999.", nameof(codigoNumerico));

        var base43 = $"{cUF:D2}{(ano % 100):D2}{mes:D2}{digits}{modelo:D2}{serie:D3}{numero:D9}{tipoEmissao}{codigoNumerico:D8}";
        return new ChaveAcesso(base43 + DigitoVerificador(base43));
    }

    public static ChaveAcesso Validar(string chave)
    {
        var d = new string((chave ?? "").Where(char.IsDigit).ToArray());
        if (d.Length != 44) throw new ArgumentException("Chave deve conter 44 dígitos.", nameof(chave));
        if (DigitoVerificador(d[..43]) != d[43])
            throw new ArgumentException("Dígito verificador da chave não confere.", nameof(chave));
        return new ChaveAcesso(d);
    }

    public static char DigitoVerificador(string base43)
    {
        if (base43.Length != 43 || base43.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Base deve conter 43 dígitos.", nameof(base43));
        var soma = 0;
        var peso = 2;
        for (var i = base43.Length - 1; i >= 0; i--)
        {
            soma += (base43[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        var resto = soma % 11;
        return (char)('0' + (resto is 0 or 1 ? 0 : 11 - resto));
    }

    public int Cuf => int.Parse(Numero[..2]);
    public int Modelo => int.Parse(Numero.Substring(20, 2));
    public int Serie => int.Parse(Numero.Substring(22, 3));
    public int NumeroDocumento => int.Parse(Numero.Substring(25, 9));

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Numero;
    }

    public override string ToString() => Numero;
}
