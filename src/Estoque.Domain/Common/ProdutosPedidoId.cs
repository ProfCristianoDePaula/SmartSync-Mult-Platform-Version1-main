namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Item do pedido (ProdutosPedido).</summary>
public readonly record struct ProdutosPedidoId(Guid Value)
{
    public static ProdutosPedidoId New() => new(Guid.NewGuid());
    public static ProdutosPedidoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
