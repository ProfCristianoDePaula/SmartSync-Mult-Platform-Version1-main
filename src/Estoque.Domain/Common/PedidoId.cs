namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Pedido (carrinho).</summary>
public readonly record struct PedidoId(Guid Value)
{
    public static PedidoId New() => new(Guid.NewGuid());
    public static PedidoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
