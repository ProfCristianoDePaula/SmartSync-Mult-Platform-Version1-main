using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>
/// Item da venda — cópia imutável de ProdutosPedido no momento do fechamento
/// (Etapa 5). Mantém IdProduto + Quantidade + FK para Venda.
/// </summary>
public sealed class ProdutosVenda : Entity<ProdutosVendaId>
{
    public VendaId IdVenda { get; private set; }
    public ProductId IdProduto { get; private set; }
    public decimal Quantidade { get; private set; }

    private ProdutosVenda() { }

    private ProdutosVenda(ProdutosVendaId id, VendaId idVenda, ProductId idProduto, decimal quantidade)
        : base(id)
    {
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade));
        IdVenda = idVenda;
        IdProduto = idProduto;
        Quantidade = Math.Round(quantidade, 4, MidpointRounding.ToEven);
    }

    public static ProdutosVenda Create(VendaId idVenda, ProductId idProduto, decimal quantidade)
        => new(ProdutosVendaId.New(), idVenda, idProduto, quantidade);

    public static ProdutosVenda FromPedido(VendaId idVenda, ProdutosPedido itemPedido)
        => new(ProdutosVendaId.New(), idVenda, itemPedido.IdProduto, itemPedido.Quantidade);
}
