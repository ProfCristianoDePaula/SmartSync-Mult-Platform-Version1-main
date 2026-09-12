namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Cupom.</summary>
public readonly record struct CupomId(Guid Value)
{
    public static CupomId New() => new(Guid.NewGuid());
    public static CupomId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
