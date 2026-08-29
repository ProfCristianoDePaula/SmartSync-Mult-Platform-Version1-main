using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence.Repositories;

public sealed class StockBalanceRepository(EstoqueDbContext dbContext) : IStockBalanceRepository
{
    public async Task AddAsync(StockBalance balance, CancellationToken ct = default)
        => await dbContext.StockBalances.AddAsync(balance, ct);

    public Task<StockBalance?> GetAsync(TenantId tenantId, Guid branchId, Guid productId, CancellationToken ct = default)
        => dbContext.StockBalances.FirstOrDefaultAsync(b =>
            b.TenantId == tenantId
            && b.BranchId == BranchId.From(branchId)
            && b.ProductId == ProductId.From(productId), ct);

    public async Task<(IReadOnlyList<StockBalance> Items, int Total)> ListAsync(
        TenantId tenantId, Guid? branchId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.StockBalances.AsNoTracking().Where(b => b.TenantId == tenantId);

        if (branchId is not null)
            query = query.Where(b => b.BranchId == BranchId.From(branchId.Value));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(b => b.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

public sealed class StockMovementRepository(EstoqueDbContext dbContext) : IStockMovementRepository
{
    public async Task AddRangeAsync(IReadOnlyList<StockMovement> movements, CancellationToken ct = default)
        => await dbContext.StockMovements.AddRangeAsync(movements, ct);

    public async Task<(IReadOnlyList<StockMovement> Items, int Total)> ListAsync(
        TenantId tenantId, Guid? branchId, Guid? productId, MovementType? type,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.StockMovements.AsNoTracking().Where(m => m.TenantId == tenantId);

        if (branchId is not null)
            query = query.Where(m => m.BranchId == BranchId.From(branchId.Value));
        if (productId is not null)
            query = query.Where(m => m.ProductId == ProductId.From(productId.Value));
        if (type is not null)
            query = query.Where(m => m.Type == type.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
