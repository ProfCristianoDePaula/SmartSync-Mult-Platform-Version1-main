namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Item de outlet.</summary>
public readonly record struct OutletItemId(Guid Value)
{
    public static OutletItemId New() => new(Guid.NewGuid());
    public static OutletItemId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
