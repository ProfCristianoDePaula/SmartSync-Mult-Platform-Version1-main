namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Movimentação de estoque.</summary>
public readonly record struct StockMovementId(Guid Value)
{
    public static StockMovementId New() => new(Guid.NewGuid());
    public static StockMovementId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
