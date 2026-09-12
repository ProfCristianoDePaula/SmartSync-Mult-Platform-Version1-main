using Estoque.Application.Common;
using Estoque.Application.PedidosVendas;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public sealed class AplicarCupomService(
    ICupomRepository cupons,
    ICupomProdutoRepository cupomProdutos,
    IProductRepository products,
    IPedidoRepository pedidos,
    IProdutosPedidoRepository itensPedido,
    IUnitOfWork uow) : IAplicarCupomService
{
    // Preço unitário mock — ponto de evolução futura: tabela de preços por produto/tenant.
    // Documentado na memória: sem preço no catálogo, assume R$ 100,00 por unidade para cálculo.
    private const decimal PrecoUnitarioMock = 100m;

    public async Task<AplicarCupomResult> AplicarAsync(AplicarCupomCommand command, CancellationToken ct = default)
    {
        var cupom = await cupons.GetAsync(command.TenantId, command.CupomId, ct);
        if (cupom is null)
            return new AplicarCupomResult(false, 0, "Cupom não encontrado.", []);

        if (cupom.EstaExpirado)
            return new AplicarCupomResult(false, 0, "Cupom expirado.", []);
        if (cupom.Quantidade <= 0)
            return new AplicarCupomResult(false, 0, "Cupom sem usos disponíveis.", []);

        var pedido = await pedidos.GetAsync(command.TenantId, command.PedidoId, ct);
        if (pedido is null)
            return new AplicarCupomResult(false, 0, "Pedido não encontrado.", []);
        if (pedido.Status != Domain.Enums.PedidoStatus.Aberto)
            return new AplicarCupomResult(false, 0, "Cupom só pode ser aplicado em pedido aberto.", []);

        var itens = await itensPedido.ListByPedidoAsync(pedido.Id, ct);
        if (itens.Count == 0)
            return new AplicarCupomResult(false, 0, "Carrinho vazio.", []);

        // Busca produtos para resolver categoria
        var produtosMap = new Dictionary<Guid, ProductId>();
        var categoriasMap = new Dictionary<Guid, Guid?>();
        foreach (var item in itens)
        {
            var prod = await products.GetAsync(command.TenantId, item.IdProduto.Value, ct);
            if (prod is null) continue;
            categoriasMap[item.IdProduto.Value] = prod.CategoryId?.Value;
        }

        var desconto = await CalcularDescontoInternoAsync(cupom, itens, categoriasMap, ct);
        if (desconto is null)
            return new AplicarCupomResult(false, 0, "Valor mínimo de compra não atingido.", []);

        // Aplica ao pedido
        pedido.AplicarCupom(cupom.Id);
        // Recalcula total com desconto aplicado será feito pelo PedidoService; aqui apenas aplica cupom ao pedido
        // Decrementa quantidade do cupom
        cupom.DecrementarUso();
        await uow.SaveChangesAsync(ct);

        var elegiveis = await ProdutosElegiveisAsync(cupom, itens, categoriasMap, ct);
        return new AplicarCupomResult(true, desconto.Value, null, elegiveis);
    }

    public async Task<decimal> CalcularDescontoAsync(TenantId tenantId, Guid cupomId, IReadOnlyList<(Guid ProdutoId, Guid? CategoriaId, decimal Quantidade, decimal PrecoUnitario)> itensCarrinho, CancellationToken ct = default)
    {
        var cupom = await cupons.GetAsync(tenantId, cupomId, ct);
        if (cupom is null) throw new BusinessRuleViolationException("Cupom não encontrado.");
        if (cupom.EstaExpirado) throw new BusinessRuleViolationException("Cupom expirado.");
        if (cupom.Quantidade <= 0) throw new BusinessRuleViolationException("Cupom sem usos disponíveis.");

        var fakeItens = itensCarrinho.Select(i => new FakeItem(ProductId.From(i.ProdutoId), i.Quantidade, i.CategoriaId, i.PrecoUnitario)).ToList();
        var categoriasMap = itensCarrinho.ToDictionary(i => i.ProdutoId, i => i.CategoriaId);
        var subtotalElegivel = await SubtotalElegivelAsync(cupom, fakeItens, categoriasMap, ct);

        if (subtotalElegivel < cupom.ValorMinimoCompra)
            throw new BusinessRuleViolationException($"Valor mínimo de compra não atingido ({cupom.ValorMinimoCompra:0.00}).");

        return CalcularValorDesconto(cupom, subtotalElegivel);
    }

    private async Task<decimal?> CalcularDescontoInternoAsync(Cupom cupom, IReadOnlyList<ProdutosPedido> itens, Dictionary<Guid, Guid?> categoriasMap, CancellationToken ct)
    {
        var subtotal = await SubtotalElegivelAsync(cupom, itens.Select(i => new FakeItem(i.IdProduto, i.Quantidade, categoriasMap.GetValueOrDefault(i.IdProduto.Value), PrecoUnitarioMock)).ToList(), categoriasMap, ct);
        if (subtotal < cupom.ValorMinimoCompra) return null;
        return CalcularValorDesconto(cupom, subtotal);
    }

    private async Task<decimal> SubtotalElegivelAsync(Cupom cupom, List<FakeItem> itens, Dictionary<Guid, Guid?> categoriasMap, CancellationToken ct)
    {
        var vinculos = await cupomProdutos.ListByCupomAsync(cupom.Id, ct);
        HashSet<Guid>? produtoIdsElegiveis = null;
        if (vinculos.Count > 0)
            produtoIdsElegiveis = vinculos.Select(v => v.IdProduto.Value).ToHashSet();

        decimal subtotal = 0;
        foreach (var item in itens)
        {
            bool elegivel;
            if (produtoIdsElegiveis is not null)
                elegivel = produtoIdsElegiveis.Contains(item.ProdutoId.Value);
            else if (cupom.IdCategoria is not null)
                elegivel = categoriasMap.TryGetValue(item.ProdutoId.Value, out var cat) && cat == cupom.IdCategoria.Value.Value;
            else
                elegivel = true; // global

            if (elegivel)
                subtotal += item.Quantidade * item.Preco;
        }
        return Math.Round(subtotal, 2, MidpointRounding.ToEven);
    }

    private async Task<IReadOnlyList<Guid>> ProdutosElegiveisAsync(Cupom cupom, IReadOnlyList<ProdutosPedido> itens, Dictionary<Guid, Guid?> categoriasMap, CancellationToken ct)
    {
        var vinculos = await cupomProdutos.ListByCupomAsync(cupom.Id, ct);
        HashSet<Guid>? produtoIds = vinculos.Count > 0 ? vinculos.Select(v => v.IdProduto.Value).ToHashSet() : null;
        var elegiveis = new List<Guid>();
        foreach (var item in itens)
        {
            bool ok;
            if (produtoIds is not null) ok = produtoIds.Contains(item.IdProduto.Value);
            else if (cupom.IdCategoria is not null) ok = categoriasMap.GetValueOrDefault(item.IdProduto.Value) == cupom.IdCategoria.Value.Value;
            else ok = true;
            if (ok) elegiveis.Add(item.IdProduto.Value);
        }
        return elegiveis;
    }

    private static decimal CalcularValorDesconto(Cupom cupom, decimal subtotalElegivel)
    {
        if (cupom.ValorDesconto > 0)
            return Math.Min(cupom.ValorDesconto, subtotalElegivel);
        // Perc
        var desc = Math.Round(subtotalElegivel * cupom.PercDesconto / 100m, 2, MidpointRounding.ToEven);
        return Math.Min(desc, subtotalElegivel);
    }

    private sealed record FakeItem(ProductId ProdutoId, decimal Quantidade, Guid? CategoriaId, decimal Preco);
}
