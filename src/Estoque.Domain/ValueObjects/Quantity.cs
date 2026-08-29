using Estoque.Domain.Common;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// Quantidade com precisão de 4 casas. Movimentações exigem valor &gt; 0;
/// saldos aceitam ≥ 0 (via FromValidated).
/// </summary>
public readonly record struct Quantity : IComparable<Quantity>
{
    public const decimal MaxValue = 999_999_999m;
    private const int Decimals = 4;

    public decimal Value { get; }

    private Quantity(decimal value) => Value = value;

    /// <summary>Cria quantidade de movimentação — deve ser estritamente positiva.</summary>
    public static Quantity Positive(decimal value, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        var rounded = Normalize(value);
        if (rounded <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", paramName ?? nameof(value));
        return new Quantity(rounded);
    }

    /// <summary>Reconstrói a partir de valor já validado/persistido (saldo pode ser zero).</summary>
    public static Quantity FromValidated(decimal value) => new(Normalize(value));

    private static decimal Normalize(decimal value)
    {
        var rounded = Math.Round(value, Decimals, MidpointRounding.ToEven);
        if (rounded is < -MaxValue or > MaxValue)
            throw new ArgumentException($"Quantidade excede o limite suportado ({MaxValue}).", nameof(value));
        return rounded;
    }

    public int CompareTo(Quantity other) => Value.CompareTo(other.Value);

    public static bool operator >(Quantity a, Quantity b) => a.Value > b.Value;
    public static bool operator <(Quantity a, Quantity b) => a.Value < b.Value;
    public static bool operator >=(Quantity a, Quantity b) => a.Value >= b.Value;
    public static bool operator <=(Quantity a, Quantity b) => a.Value <= b.Value;
    public static Quantity operator +(Quantity a, Quantity b) => FromValidated(a.Value + b.Value);
    public static Quantity operator -(Quantity a, Quantity b) => FromValidated(a.Value - b.Value);

    public override string ToString() => Value.ToString("0.####");
}
