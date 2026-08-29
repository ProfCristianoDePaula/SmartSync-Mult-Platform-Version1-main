namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Modelo.</summary>
public readonly record struct ModelId(Guid Value)
{
    public static ModelId New() => new(Guid.NewGuid());
    public static ModelId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
