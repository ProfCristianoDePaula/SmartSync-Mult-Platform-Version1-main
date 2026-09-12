using Estoque.Application.Common;
using Estoque.Application.PedidosVendas;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;

namespace Estoque.Infrastructure.Services.PedidosVendas;

public sealed class PedidoService(
    IPedidoRepository pedidos,
    IProdutosPedidoRepository itensPedido,
    IProductRepository products,
    ICupomRepository cupons,
    ICupomProdutoRepository cupomProdutos,
    IUnitOfWork uow) : IPedidoService
{
    private const decimal PrecoMock = 100m;

    public async Task<PedidoDto> GetOrCreateCarrinhoAsync(GetOrCreateCarrinhoCommand command, CancellationToken ct = default)
    {
        BranchId? unidade = command.IdUnidade is null ? null : BranchId.From(command.IdUnidade.Value);
        var carrinho = await pedidos.GetCarrinhoAbertoAsync(command.TenantId, command.ClienteId, unidade, ct);
        if (carrinho is not null)
        {
            var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
            return await ToDtoAsync(carrinho, itens, ct);
        }

        var novo = Pedido.CriarCarrinho(command.TenantId, command.ClienteId, unidade);
        await pedidos.AddAsync(novo, ct);
        await uow.SaveChangesAsync(ct);
        return await ToDtoAsync(novo, [], ct);
    }

    public async Task<PedidoDto> AdicionarItemAsync(AdicionarItemCommand command, CancellationToken ct = default)
    {
        var prod = await products.GetAsync(command.TenantId, command.ProdutoId, ct);
        if (prod is null || !prod.IsActive)
            throw new BusinessRuleViolationException("Produto não encontrado.");

        BranchId? unidade = command.IdUnidade is null ? null : BranchId.From(command.IdUnidade.Value);
        var carrinho = await pedidos.GetCarrinhoAbertoAsync(command.TenantId, command.ClienteId, unidade, ct);
        if (carrinho is null)
        {
            carrinho = Pedido.CriarCarrinho(command.TenantId, command.ClienteId, unidade);
            await pedidos.AddAsync(carrinho, ct);
            await uow.SaveChangesAsync(ct);
        }

        var existente = await itensPedido.GetByPedidoProdutoAsync(carrinho.Id, ProductId.From(command.ProdutoId), ct);
        if (existente is not null)
        {
            existente.AlterarQuantidade(existente.Quantidade + command.Quantidade);
        }
        else
        {
            var novoItem = ProdutosPedido.Create(carrinho.Id, ProductId.From(command.ProdutoId), command.Quantidade);
            await itensPedido.AddAsync(novoItem, ct);
        }
        await uow.SaveChangesAsync(ct);
        await RecalcularValorTotalAsync(carrinho, ct);
        await uow.SaveChangesAsync(ct);

        var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        return await ToDtoAsync(carrinho, itens, ct);
    }

    public async Task<PedidoDto> RemoverItemAsync(RemoverItemCommand command, CancellationToken ct = default)
    {
        BranchId? unidade = command.IdUnidade is null ? null : BranchId.From(command.IdUnidade.Value);
        var carrinho = await pedidos.GetCarrinhoAbertoAsync(command.TenantId, command.ClienteId, unidade, ct);
        if (carrinho is null)
            throw new BusinessRuleViolationException("Carrinho não encontrado.");

        var item = await itensPedido.GetByPedidoProdutoAsync(carrinho.Id, ProductId.From(command.ProdutoId), ct);
        if (item is null)
            throw new BusinessRuleViolationException("Item não encontrado no carrinho.");

        itensPedido.Remove(item);
        await uow.SaveChangesAsync(ct);
        await RecalcularValorTotalAsync(carrinho, ct);
        await uow.SaveChangesAsync(ct);

        var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        return await ToDtoAsync(carrinho, itens, ct);
    }

    public async Task<PedidoDto> AplicarCupomAsync(AplicarCupomCarrinhoCommand command, CancellationToken ct = default)
    {
        BranchId? unidade = command.IdUnidade is null ? null : BranchId.From(command.IdUnidade.Value);
        var carrinho = await pedidos.GetCarrinhoAbertoAsync(command.TenantId, command.ClienteId, unidade, ct);
        if (carrinho is null)
            throw new BusinessRuleViolationException("Carrinho não encontrado.");

        var cupom = await cupons.GetAsync(command.TenantId, command.CupomId, ct);
        if (cupom is null) throw new BusinessRuleViolationException("Cupom não encontrado.");
        if (cupom.EstaExpirado) throw new BusinessRuleViolationException("Cupom expirado.");
        if (cupom.Quantidade <= 0) throw new BusinessRuleViolationException("Cupom sem usos disponíveis.");

        var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        if (itens.Count == 0) throw new BusinessRuleViolationException("Carrinho vazio.");

        // valida elegibilidade / valor mínimo (mesma lógica do AplicarCupomService)
        var desconto = await CalcularDescontoParaPedidoAsync(cupom, itens, command.TenantId, ct);
        if (desconto is null)
            throw new BusinessRuleViolationException($"Valor mínimo de compra não atingido ({cupom.ValorMinimoCompra:0.00}).");

        carrinho.AplicarCupom(cupom.Id);
        // Não decrementa aqui — decremento ocorre no fechamento (Etapa 5) para evitar consumo indevido; por enquanto apenas vincula
        await RecalcularValorTotalAsync(carrinho, ct);
        await uow.SaveChangesAsync(ct);

        var novosItens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        return await ToDtoAsync(carrinho, novosItens, ct);
    }

    public async Task<PedidoDto?> ConsultarCarrinhoAsync(ConsultarCarrinhoQuery query, CancellationToken ct = default)
    {
        BranchId? unidade = query.IdUnidade is null ? null : BranchId.From(query.IdUnidade.Value);
        var carrinho = await pedidos.GetCarrinhoAbertoAsync(query.TenantId, query.ClienteId, unidade, ct);
        if (carrinho is null) return null;
        var itens = await itensPedido.ListByPedidoAsync(carrinho.Id, ct);
        return await ToDtoAsync(carrinho, itens, ct);
    }

    public async Task<PagedResult<PedidoDto>> ListAsync(ListPedidosQuery query, CancellationToken ct = default)
    {
        BranchId? unidade = query.IdUnidade is null ? null : BranchId.From(query.IdUnidade.Value);
        var (items, total) = await pedidos.ListAsync(query.TenantId, query.IdCliente, unidade, query.Status, query.Page, query.PageSize, ct);
        var dtos = new List<PedidoDto>();
        foreach (var p in items)
        {
            var itens = await itensPedido.ListByPedidoAsync(p.Id, ct);
            dtos.Add(await ToDtoAsync(p, itens, ct));
        }
        return new PagedResult<PedidoDto>(dtos, query.Page, query.PageSize, total, (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<PedidoDto?> GetByIdAsync(TenantId tenantId, Guid pedidoId, CancellationToken ct = default)
    {
        var pedido = await pedidos.GetAsync(tenantId, pedidoId, ct);
        if (pedido is null) return null;
        var itens = await itensPedido.ListByPedidoAsync(pedido.Id, ct);
        return await ToDtoAsync(pedido, itens, ct);
    }

    public async Task<PedidoDto> CancelarAsync(TenantId tenantId, Guid pedidoId, CancellationToken ct = default)
    {
        var pedido = await pedidos.GetAsync(tenantId, pedidoId, ct);
        if (pedido is null) throw new BusinessRuleViolationException("Pedido não encontrado.");
        if (pedido.Status == Domain.Enums.PedidoStatus.Cancelado)
            throw new BusinessRuleViolationException("Pedido já está cancelado.");
        if (pedido.Status == Domain.Enums.PedidoStatus.VendaEfetuada)
            throw new BusinessRuleViolationException("Pedido já fechado como venda não pode ser cancelado (necessário estorno).");
        // Permite cancelar Aberto, AguardandoPagamento, PagamentoAprovado
        pedido.Fechar(Domain.Enums.PedidoStatus.Cancelado);
        await uow.SaveChangesAsync(ct);
        var itens = await itensPedido.ListByPedidoAsync(pedido.Id, ct);
        return await ToDtoAsync(pedido, itens, ct);
    }

    private async Task RecalcularValorTotalAsync(Pedido pedido, CancellationToken ct)
    {
        var itens = await itensPedido.ListByPedidoAsync(pedido.Id, ct);
        decimal subtotal = itens.Sum(i => i.Quantidade * PrecoMock);
        decimal desconto = 0;
        if (pedido.IdCupom is not null)
        {
            var cupom = await cupons.GetAsync(pedido.TenantId, pedido.IdCupom.Value.Value, ct);
            if (cupom is not null)
            {
                var d = await CalcularDescontoParaPedidoAsync(cupom, itens, pedido.TenantId, ct);
                if (d is not null) desconto = d.Value;
                else
                {
                    // Cupom não atinge mínimo mais — remove vínculo (comportamento idempotente)
                    pedido.RemoverCupom();
                }
            }
        }
        var total = Math.Max(0, subtotal - desconto);
        pedido.AtualizarValorTotal(total);
    }

    private async Task<decimal?> CalcularDescontoParaPedidoAsync(Cupom cupom, IReadOnlyList<ProdutosPedido> itens, TenantId tenantId, CancellationToken ct)
    {
        // Resolve categorias
        var catMap = new Dictionary<Guid, Guid?>();
        foreach (var item in itens)
        {
            var prod = await products.GetAsync(tenantId, item.IdProduto.Value, ct);
            catMap[item.IdProduto.Value] = prod?.CategoryId?.Value;
        }
        var vinculos = await cupomProdutos.ListByCupomAsync(cupom.Id, ct);
        HashSet<Guid>? elegiveis = vinculos.Count > 0 ? vinculos.Select(v => v.IdProduto.Value).ToHashSet() : null;

        decimal elegivel = 0;
        foreach (var item in itens)
        {
            bool ok;
            if (elegiveis is not null) ok = elegiveis.Contains(item.IdProduto.Value);
            else if (cupom.IdCategoria is not null) ok = catMap.GetValueOrDefault(item.IdProduto.Value) == cupom.IdCategoria.Value.Value;
            else ok = true;
            if (ok) elegivel += item.Quantidade * PrecoMock;
        }
        elegivel = Math.Round(elegivel, 2);
        if (elegivel < cupom.ValorMinimoCompra) return null;
        if (cupom.ValorDesconto > 0) return Math.Min(cupom.ValorDesconto, elegivel);
        return Math.Round(elegivel * cupom.PercDesconto / 100m, 2);
    }

    private async Task<PedidoDto> ToDtoAsync(Pedido p, IReadOnlyList<ProdutosPedido> itens, CancellationToken ct)
    {
        var itemDtos = new List<ProdutosPedidoDto>();
        foreach (var item in itens)
        {
            var prod = await products.GetAsync(p.TenantId, item.IdProduto.Value, ct);
            itemDtos.Add(new ProdutosPedidoDto(item.Id.Value, item.IdProduto.Value, item.Quantidade, prod?.Sku.Value ?? "?", prod?.Name ?? "?"));
        }

        decimal subtotalBruto = itens.Sum(i => i.Quantidade * PrecoMock);
        decimal desconto = subtotalBruto - p.ValorTotal;
        if (desconto < 0) desconto = 0;

        return new PedidoDto(
            p.Id.Value, p.DataAbertura, p.IdCliente, p.IdCupom?.Value, p.IdUnidade?.Value,
            p.Status, p.DataFechamento, p.ValorTotal, itemDtos, Math.Round(desconto,2), 0);
    }
}
