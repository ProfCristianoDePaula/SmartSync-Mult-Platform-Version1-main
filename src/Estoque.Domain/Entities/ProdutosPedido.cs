using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>
/// Item do pedido/carrinho. Propriedade <c>IdPedido</c> é o nome canônico escolhido
/// para o FK (decisão Etapa 1): o spec original usava <c>IdCarrinho</c>, que é
/// sinônimo de IdPedido (carrinho = Pedido com Status Aberto). A renomeação traz
/// consistência com <c>ProdutosVenda.IdVenda</c> e com o restante do domínio
/// (BrandId, ProductId...). Não há coluna legada a migrar pois é novo módulo.
/// </summary>
public sealed class ProdutosPedido : Entity<ProdutosPedidoId>
{
    /// <summary>FK para Pedido — nome legado no spec: IdCarrinho.</summary>
    public PedidoId IdPedido { get; private set; }

    /// <summary>Alias de compatibilidade para leitura do spec — retorna IdPedido.</summary>
    public PedidoId IdCarrinho => IdPedido;

    public ProductId IdProduto { get; private set; }
    public decimal Quantidade { get; private set; }

    private ProdutosPedido() { }

    private ProdutosPedido(ProdutosPedidoId id, PedidoId idPedido, ProductId idProduto, decimal quantidade)
        : base(id)
    {
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        IdPedido = idPedido;
        IdProduto = idProduto;
        Quantidade = Math.Round(quantidade, 4, MidpointRounding.ToEven);
    }

    public static ProdutosPedido Create(PedidoId idPedido, ProductId idProduto, decimal quantidade)
        => new(ProdutosPedidoId.New(), idPedido, idProduto, quantidade);

    public static ProdutosPedido FromIds(Guid idPedido, Guid idProduto, decimal quantidade)
        => new(ProdutosPedidoId.New(), PedidoId.From(idPedido), ProductId.From(idProduto), quantidade);

    public void AlterarQuantidade(decimal novaQuantidade)
    {
        if (novaQuantidade <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(novaQuantidade));
        Quantidade = Math.Round(novaQuantidade, 4, MidpointRounding.ToEven);
    }
}
