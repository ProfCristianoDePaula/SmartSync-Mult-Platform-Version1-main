namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Venda.</summary>
public readonly record struct VendaId(Guid Value)
{
    public static VendaId New() => new(Guid.NewGuid());
    public static VendaId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
