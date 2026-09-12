namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — FormaPagto.</summary>
public readonly record struct FormaPagtoId(Guid Value)
{
    public static FormaPagtoId New() => new(Guid.NewGuid());
    public static FormaPagtoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
