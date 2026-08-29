using Identity.Domain.Common;
using System.Text.RegularExpressions;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// CNPJ (Brazilian company registration) with validation performed at
/// construction time — an invalid CNPJ can never exist in memory.
/// </summary>
public sealed partial class Cnpj : ValueObject
{
    private static readonly int[] FirstCheckWeights = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] SecondCheckWeights = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    public string Number { get; }

    private Cnpj(string number) => Number = number;

    public static Cnpj Create(string? value)
    {
        var number = Normalize(value);

        if (number.All(c => c == number[0]))
            throw new ArgumentException("CNPJ inválido: todos os dígitos são iguais.", nameof(value));

        if (CalculateCheckDigit(number.AsSpan(0, 12), FirstCheckWeights) != number[12] ||
            CalculateCheckDigit(number.AsSpan(0, 13), SecondCheckWeights) != number[13])
        {
            throw new ArgumentException("CNPJ inválido: dígitos verificadores não conferem.", nameof(value));
        }

        return new Cnpj(number);
    }

    /// <summary>
    /// Rebuilds a CNPJ from an already-validated, normalized number
    /// (e.g. when loading from the database). Does not re-validate.
    /// </summary>
    public static Cnpj FromValidated(string number) => new(number);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CNPJ é obrigatório.", nameof(value));

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (!DigitsRegex().IsMatch(digits))
            throw new ArgumentException("CNPJ deve conter 14 dígitos numéricos.", nameof(value));

        return digits;
    }

    private static char CalculateCheckDigit(ReadOnlySpan<char> digits, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
            sum += (digits[i] - '0') * weights[i];

        var rest = sum % 11;
        var digit = rest < 2 ? 0 : 11 - rest;
        return (char)('0' + digit);
    }

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Number;
    }

    public override string ToString()
        => $"{Number[..2]}.{Number.Substring(2, 3)}.{Number.Substring(5, 3)}/{Number.Substring(8, 4)}-{Number[12..]}";

    [GeneratedRegex(@"^\d{14}$")]
    private static partial Regex DigitsRegex();
}
