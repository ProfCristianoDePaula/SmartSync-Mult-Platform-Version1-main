using Estoque.Domain.Common;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// SKU (código interno do produto) — normalizado em maiúsculas, único por
/// tenant entre produtos ativos.
/// </summary>
public sealed class Sku : ValueObject
{
    public const int MaxLength = 64;

    public string Value { get; }

    private Sku(string value) => Value = value;

    public static Sku Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU é obrigatório.", nameof(value));

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > MaxLength)
            throw new ArgumentException($"SKU excede o tamanho máximo de {MaxLength} caracteres.", nameof(value));

        return new Sku(normalized);
    }

    public static Sku FromValidated(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
