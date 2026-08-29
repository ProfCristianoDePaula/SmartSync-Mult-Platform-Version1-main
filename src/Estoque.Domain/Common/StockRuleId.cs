namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Regra de estoque.</summary>
public readonly record struct StockRuleId(Guid Value)
{
    public static StockRuleId New() => new(Guid.NewGuid());
    public static StockRuleId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
