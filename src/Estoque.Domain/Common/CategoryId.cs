namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Categoria.</summary>
public readonly record struct CategoryId(Guid Value)
{
    public static CategoryId New() => new(Guid.NewGuid());
    public static CategoryId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
