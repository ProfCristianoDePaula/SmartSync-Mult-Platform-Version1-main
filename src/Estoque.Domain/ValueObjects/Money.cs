using Estoque.Domain.Common;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// Valor monetário (BRL) não negativo — custo unitário, custo médio.
/// </summary>
public readonly record struct Money
{
    public const decimal MaxValue = 99_999_999m;
    private const int Decimals = 2;

    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Money Create(decimal value)
    {
        var rounded = Math.Round(value, Decimals, MidpointRounding.ToEven);
        if (rounded is < 0 or > MaxValue)
            throw new ArgumentException("Valor monetário deve estar entre 0 e 99.999.999.", nameof(value));
        return new Money(rounded);
    }

    public static Money FromValidated(decimal value) => new(value);

    public override string ToString() => Amount.ToString("0.00");
}
