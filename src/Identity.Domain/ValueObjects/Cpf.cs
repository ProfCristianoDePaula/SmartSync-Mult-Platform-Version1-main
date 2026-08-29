using Identity.Domain.Common;
using System.Text.RegularExpressions;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// CPF (Brazilian individual tax id) with validation performed at
/// construction time — an invalid CPF can never exist in memory.
/// </summary>
public sealed partial class Cpf : ValueObject
{
    private static readonly int[] FirstCheckWeights = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] SecondCheckWeights = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

    public string Number { get; }

    private Cpf(string number) => Number = number;

    public static Cpf Create(string? value)
    {
        var number = Normalize(value);

        if (number.All(c => c == number[0]))
            throw new ArgumentException("CPF inválido: todos os dígitos são iguais.", nameof(value));

        if (CalculateCheckDigit(number.AsSpan(0, 9), FirstCheckWeights) != number[9] ||
            CalculateCheckDigit(number.AsSpan(0, 10), SecondCheckWeights) != number[10])
        {
            throw new ArgumentException("CPF inválido: dígitos verificadores não conferem.", nameof(value));
        }

        return new Cpf(number);
    }

    /// <summary>
    /// Rebuilds a CPF from an already-validated, normalized number
    /// (e.g. when loading from the database). Does not re-validate.
    /// </summary>
    public static Cpf FromValidated(string number) => new(number);

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CPF é obrigatório.", nameof(value));

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (!DigitsRegex().IsMatch(digits))
            throw new ArgumentException("CPF deve conter 11 dígitos numéricos.", nameof(value));

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
        => $"{Number[..3]}.{Number.Substring(3, 3)}.{Number.Substring(6, 3)}-{Number[9..]}";

    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex DigitsRegex();
}
