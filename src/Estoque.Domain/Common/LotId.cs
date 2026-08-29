namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Lote (validade).</summary>
public readonly record struct LotId(Guid Value)
{
    public static LotId New() => new(Guid.NewGuid());
    public static LotId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
