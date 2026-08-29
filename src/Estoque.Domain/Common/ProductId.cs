namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Produto.</summary>
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
    public static ProductId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
