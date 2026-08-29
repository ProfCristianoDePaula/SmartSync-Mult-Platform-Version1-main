using Estoque.Domain.Common;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// Código de barras EAN-13 com validação do dígito verificador.
/// </summary>
public sealed class Barcode : ValueObject
{
    public string Value { get; }

    private Barcode(string value) => Value = value;

    public static Barcode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Código de barras é obrigatório.", nameof(value));

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length != 13)
            throw new ArgumentException("Código de barras deve conter 13 dígitos (EAN-13).", nameof(value));

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);

        var check = (10 - (sum % 10)) % 10;
        if ((digits[12] - '0') != check)
            throw new ArgumentException("Código de barras inválido: dígito verificador não confere.", nameof(value));

        return new Barcode(digits);
    }

    public static Barcode FromValidated(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
