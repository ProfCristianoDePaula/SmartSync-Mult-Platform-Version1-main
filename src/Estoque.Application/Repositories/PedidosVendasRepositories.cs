using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;

namespace Estoque.Application.Repositories;

public interface ICupomRepository
{
    Task<Cupom?> GetAsync(TenantId tenantId, Guid cupomId, CancellationToken ct = default);
    Task<Cupom?> GetByDescricaoAsync(TenantId tenantId, string descricao, CancellationToken ct = default);
    Task AddAsync(Cupom cupom, CancellationToken ct = default);
    void Remove(Cupom cupom);
    Task<(IReadOnlyList<Cupom> Items, int Total)> ListAsync(TenantId tenantId, string? search, bool? apenasValidos, int page, int pageSize, CancellationToken ct = default);
}

public interface ICupomProdutoRepository
{
    Task<IReadOnlyList<CupomProduto>> ListByCupomAsync(CupomId cupomId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<CupomProduto> vinculos, CancellationToken ct = default);
    Task RemoveByCupomAsync(CupomId cupomId, CancellationToken ct = default);
    Task<bool> ExistsAsync(CupomId cupomId, ProductId produtoId, CancellationToken ct = default);
}

public interface IPedidoRepository
{
    Task<Pedido?> GetAsync(TenantId tenantId, Guid pedidoId, CancellationToken ct = default);
    Task<Pedido?> GetCarrinhoAbertoAsync(TenantId tenantId, Guid clienteId, BranchId? unidadeId, CancellationToken ct = default);
    Task AddAsync(Pedido pedido, CancellationToken ct = default);
    Task<(IReadOnlyList<Pedido> Items, int Total)> ListAsync(TenantId tenantId, Guid? clienteId, BranchId? unidadeId, PedidoStatus? status, int page, int pageSize, CancellationToken ct = default);
}

public interface IProdutosPedidoRepository
{
    Task<IReadOnlyList<ProdutosPedido>> ListByPedidoAsync(PedidoId pedidoId, CancellationToken ct = default);
    Task<ProdutosPedido?> GetByPedidoProdutoAsync(PedidoId pedidoId, ProductId produtoId, CancellationToken ct = default);
    Task AddAsync(ProdutosPedido item, CancellationToken ct = default);
    void Remove(ProdutosPedido item);
    void RemoveRange(IEnumerable<ProdutosPedido> itens);
}

public interface IVendaRepository
{
    Task<Venda?> GetAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default);
    Task<Venda?> GetByPedidoAsync(PedidoId pedidoId, CancellationToken ct = default);
    Task AddAsync(Venda venda, CancellationToken ct = default);
    Task<(IReadOnlyList<Venda> Items, int Total)> ListAsync(TenantId tenantId, Guid? clienteIdViaPedido, BranchId? unidadeId, int page, int pageSize, CancellationToken ct = default);
}

public interface IProdutosVendaRepository
{
    Task<IReadOnlyList<ProdutosVenda>> ListByVendaAsync(VendaId vendaId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<ProdutosVenda> itens, CancellationToken ct = default);
}

public interface IFormaPagtoRepository
{
    Task<FormaPagto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<FormaPagto?> GetByDescricaoAsync(string descricao, CancellationToken ct = default);
    Task AddAsync(FormaPagto fp, CancellationToken ct = default);
    Task<IReadOnlyList<FormaPagto>> ListAsync(CancellationToken ct = default);
}
