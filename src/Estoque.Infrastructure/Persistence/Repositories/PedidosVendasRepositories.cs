using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence.Repositories;

public sealed class CupomRepository(EstoqueDbContext db) : ICupomRepository
{
    public Task<Cupom?> GetAsync(TenantId tenantId, Guid cupomId, CancellationToken ct = default)
        => db.Cupons.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == CupomId.From(cupomId), ct);

    public Task<Cupom?> GetByDescricaoAsync(TenantId tenantId, string descricao, CancellationToken ct = default)
        => db.Cupons.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Descricao == descricao.Trim(), ct);

    public async Task AddAsync(Cupom cupom, CancellationToken ct = default)
        => await db.Cupons.AddAsync(cupom, ct);

    public void Remove(Cupom cupom) => db.Cupons.Remove(cupom);

    public async Task<(IReadOnlyList<Cupom> Items, int Total)> ListAsync(TenantId tenantId, string? search, bool? apenasValidos, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Cupons.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Descricao.Contains(search));
        if (apenasValidos == true)
            query = query.Where(c => c.DataValidade >= DateTime.UtcNow && c.Quantidade > 0);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.DataCriacao)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class CupomProdutoRepository(EstoqueDbContext db) : ICupomProdutoRepository
{
    public async Task<IReadOnlyList<CupomProduto>> ListByCupomAsync(CupomId cupomId, CancellationToken ct = default)
        => await db.CupomProdutos.AsNoTracking().Where(x => x.IdCupom == cupomId).ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<CupomProduto> vinculos, CancellationToken ct = default)
        => await db.CupomProdutos.AddRangeAsync(vinculos, ct);

    public async Task RemoveByCupomAsync(CupomId cupomId, CancellationToken ct = default)
    {
        var existentes = await db.CupomProdutos.Where(x => x.IdCupom == cupomId).ToListAsync(ct);
        db.CupomProdutos.RemoveRange(existentes);
    }

    public Task<bool> ExistsAsync(CupomId cupomId, ProductId produtoId, CancellationToken ct = default)
        => db.CupomProdutos.AnyAsync(x => x.IdCupom == cupomId && x.IdProduto == produtoId, ct);
}

public sealed class PedidoRepository(EstoqueDbContext db) : IPedidoRepository
{
    public Task<Pedido?> GetAsync(TenantId tenantId, Guid pedidoId, CancellationToken ct = default)
        => db.Pedidos.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == PedidoId.From(pedidoId), ct);

    public Task<Pedido?> GetCarrinhoAbertoAsync(TenantId tenantId, Guid clienteId, BranchId? unidadeId, CancellationToken ct = default)
    {
        var query = db.Pedidos.Where(p => p.TenantId == tenantId && p.IdCliente == clienteId && p.Status == PedidoStatus.Aberto);
        if (unidadeId is not null)
            query = query.Where(p => p.IdUnidade == unidadeId);
        // Se unidade nula, pega qualquer carrinho aberto do cliente no tenant (compatível com spec nullable)
        return query.OrderByDescending(p => p.DataAbertura).FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Pedido pedido, CancellationToken ct = default)
        => await db.Pedidos.AddAsync(pedido, ct);

    public async Task<(IReadOnlyList<Pedido> Items, int Total)> ListAsync(TenantId tenantId, Guid? clienteId, BranchId? unidadeId, PedidoStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Pedidos.AsNoTracking().Where(p => p.TenantId == tenantId);
        if (clienteId is not null) query = query.Where(p => p.IdCliente == clienteId);
        if (unidadeId is not null) query = query.Where(p => p.IdUnidade == unidadeId);
        if (status is not null) query = query.Where(p => p.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(p => p.DataAbertura)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class ProdutosPedidoRepository(EstoqueDbContext db) : IProdutosPedidoRepository
{
    public async Task<IReadOnlyList<ProdutosPedido>> ListByPedidoAsync(PedidoId pedidoId, CancellationToken ct = default)
        => await db.ProdutosPedidos.AsNoTracking().Where(x => x.IdPedido == pedidoId).ToListAsync(ct);

    public Task<ProdutosPedido?> GetByPedidoProdutoAsync(PedidoId pedidoId, ProductId produtoId, CancellationToken ct = default)
        => db.ProdutosPedidos.FirstOrDefaultAsync(x => x.IdPedido == pedidoId && x.IdProduto == produtoId, ct);

    public async Task AddAsync(ProdutosPedido item, CancellationToken ct = default)
        => await db.ProdutosPedidos.AddAsync(item, ct);

    public void Remove(ProdutosPedido item) => db.ProdutosPedidos.Remove(item);
    public void RemoveRange(IEnumerable<ProdutosPedido> itens) => db.ProdutosPedidos.RemoveRange(itens);
}

public sealed class VendaRepository(EstoqueDbContext db) : IVendaRepository
{
    public Task<Venda?> GetAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default)
        => db.Vendas.FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Id == VendaId.From(vendaId), ct);

    public Task<Venda?> GetByPedidoAsync(PedidoId pedidoId, CancellationToken ct = default)
        => db.Vendas.FirstOrDefaultAsync(v => v.IdPedido == pedidoId, ct);

    public async Task AddAsync(Venda venda, CancellationToken ct = default)
        => await db.Vendas.AddAsync(venda, ct);

    public async Task<(IReadOnlyList<Venda> Items, int Total)> ListAsync(TenantId tenantId, Guid? clienteIdViaPedido, BranchId? unidadeId, int page, int pageSize, CancellationToken ct = default)
    {
        // Para filtro por cliente/unidade, precisamos join via Pedidos — faz via subquery
        var query = db.Vendas.AsNoTracking().Where(v => v.TenantId == tenantId);

        if (clienteIdViaPedido is not null || unidadeId is not null)
        {
            var pedidoIds = db.Pedidos.AsNoTracking().Where(p => p.TenantId == tenantId);
            if (clienteIdViaPedido is not null) pedidoIds = pedidoIds.Where(p => p.IdCliente == clienteIdViaPedido);
            if (unidadeId is not null) pedidoIds = pedidoIds.Where(p => p.IdUnidade == unidadeId);
            var ids = await pedidoIds.Select(p => p.Id).ToListAsync(ct);
            var idSet = ids.Select(id => id.Value).ToHashSet();
            // Filtra vendas cujo IdPedido está no conjunto
            query = query.Where(v => idSet.Contains(v.IdPedido.Value));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(v => v.DataVenda)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class ProdutosVendaRepository(EstoqueDbContext db) : IProdutosVendaRepository
{
    public async Task<IReadOnlyList<ProdutosVenda>> ListByVendaAsync(VendaId vendaId, CancellationToken ct = default)
        => await db.ProdutosVendas.AsNoTracking().Where(x => x.IdVenda == vendaId).ToListAsync(ct);

    public async Task AddRangeAsync(IEnumerable<ProdutosVenda> itens, CancellationToken ct = default)
        => await db.ProdutosVendas.AddRangeAsync(itens, ct);
}

public sealed class FormaPagtoRepository(EstoqueDbContext db) : IFormaPagtoRepository
{
    public Task<FormaPagto?> GetAsync(Guid id, CancellationToken ct = default)
        => db.FormasPagto.FirstOrDefaultAsync(f => f.Id == FormaPagtoId.From(id), ct);

    public Task<FormaPagto?> GetByDescricaoAsync(string descricao, CancellationToken ct = default)
        => db.FormasPagto.FirstOrDefaultAsync(f => f.Descricao == descricao.Trim(), ct);

    public async Task AddAsync(FormaPagto fp, CancellationToken ct = default)
        => await db.FormasPagto.AddAsync(fp, ct);

    public async Task<IReadOnlyList<FormaPagto>> ListAsync(CancellationToken ct = default)
        => await db.FormasPagto.AsNoTracking().OrderBy(f => f.Descricao).ToListAsync(ct);
}
