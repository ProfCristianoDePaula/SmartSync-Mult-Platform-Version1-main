namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Marca.</summary>
public readonly record struct BrandId(Guid Value)
{
    public static BrandId New() => new(Guid.NewGuid());
    public static BrandId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
