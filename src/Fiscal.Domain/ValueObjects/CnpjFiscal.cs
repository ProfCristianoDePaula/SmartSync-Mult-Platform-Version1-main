using Fiscal.Domain.Common;

namespace Fiscal.Domain.ValueObjects;

/// <summary>
/// CNPJ do emitente: 14 dígitos com DV módulo 11, ou alfanumérico
/// [0-9A-Z]{14} (NT Conjunta 2025.001 — algoritmo do DV pendente de
/// confirmação, ver PENDENCIAS.md F0-01; alfanumérico entra sinalizado).
/// CPF (11 dígitos) é recusado: tenant pessoa física fora da v1 (P3 → 422).
/// </summary>
public sealed class CnpjFiscal : ValueObject
{
    private static readonly int[] W1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] W2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    public string Numero { get; }
    public bool Alfanumerico { get; }

    private CnpjFiscal(string numero, bool alfanumerico)
    {
        Numero = numero;
        Alfanumerico = alfanumerico;
    }

    public static CnpjFiscal Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CNPJ do emitente é obrigatório.", nameof(value));

        var norm = new string(value.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();

        if (norm.Length == 11 && norm.All(char.IsDigit))
            throw new BusinessRuleViolationException(
                "Emitente pessoa física (CPF) fora do escopo da v1 fiscal. Cadastre um emitente CNPJ.",
                "fiscal.emitente.cpf-bloqueado");

        if (norm.Length != 14 || !norm.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'Z')))
            throw new ArgumentException("CNPJ deve conter 14 caracteres [0-9A-Z].", nameof(value));

        if (norm.All(c => c == norm[0]))
            throw new ArgumentException("CNPJ inválido: todos os caracteres são iguais.", nameof(value));

        if (norm.All(char.IsDigit))
        {
            if (Dv(norm.AsSpan(0, 12), W1) != norm[12] || Dv(norm.AsSpan(0, 13), W2) != norm[13])
                throw new ArgumentException("CNPJ inválido: dígitos verificadores não conferem.", nameof(value));
            return new CnpjFiscal(norm, false);
        }

        return new CnpjFiscal(norm, true);
    }

    public static CnpjFiscal FromValidated(string numero, bool alfanumerico) => new(numero, alfanumerico);

    /// <summary>Primeiros 8 dígitos (ou caracteres) = CNPJ-base (matriz).</summary>
    public string Base => Numero[..8];

    private static char Dv(ReadOnlySpan<char> digits, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
            sum += (digits[i] - '0') * weights[i];
        var rest = sum % 11;
        return (char)('0' + (rest < 2 ? 0 : 11 - rest));
    }

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Numero;
    }
}
