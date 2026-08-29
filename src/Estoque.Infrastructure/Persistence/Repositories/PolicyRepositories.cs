using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence.Repositories;

public sealed class StockRuleRepository(EstoqueDbContext dbContext) : IStockRuleRepository
{
    public async Task AddAsync(StockRule rule, CancellationToken ct = default)
        => await dbContext.StockRules.AddAsync(rule, ct);

    public Task<StockRule?> FindExactAsync(TenantId tenantId, Guid productId, Guid? branchId, CancellationToken ct = default)
    {
        // Valor convertido calculado FORA da árvore de expressão (EF não
        // traduz padrões 'is').
        Estoque.Domain.Common.BranchId? branchFilter =
            branchId is null ? null : Estoque.Domain.Common.BranchId.From(branchId.Value);

        return dbContext.StockRules.FirstOrDefaultAsync(r =>
            r.TenantId == tenantId
            && r.ProductId == Estoque.Domain.Common.ProductId.From(productId)
            && r.BranchId == branchFilter, ct);
    }

    /// <summary>Regra EFETIVA: override da filial tem prioridade; senão a regra padrão do tenant.</summary>
    public async Task<StockRule?> GetEffectiveAsync(TenantId tenantId, ProductId productId, BranchId? branchId, CancellationToken ct = default)
    {
        if (branchId is not null)
        {
            var specific = await FindExactAsync(tenantId, productId.Value, branchId.Value.Value, ct);
            if (specific is not null)
                return specific;
        }

        return await FindExactAsync(tenantId, productId.Value, null, ct);
    }

    public async Task<(IReadOnlyList<StockRule> Items, int Total)> ListAsync(
        TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.StockRules.AsNoTracking().Where(r => r.TenantId == tenantId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.ProductId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }
}

public sealed class LotRepository(EstoqueDbContext dbContext) : ILotRepository
{
    public async Task AddAsync(Lot lot, CancellationToken ct = default)
        => await dbContext.Lots.AddAsync(lot, ct);

    public Task<Lot?> GetAsync(TenantId tenantId, Guid lotId, CancellationToken ct = default)
        => dbContext.Lots.FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == LotId.From(lotId), ct);

    public Task<Lot?> GetByNumberAsync(TenantId tenantId, Guid branchId, Guid productId, string number, CancellationToken ct = default)
        => dbContext.Lots.FirstOrDefaultAsync(l =>
            l.TenantId == tenantId
            && l.BranchId == BranchId.From(branchId)
            && l.ProductId == ProductId.From(productId)
            && l.Number == number.Trim(), ct);

    public Task<IReadOnlyList<Lot>> ListExpiringAsync(
        TenantId tenantId, DateOnly today, int windowDays, Guid? branchId, CancellationToken ct = default)
    {
        // Vencendo nos próximos N dias OU já vencido e ainda com quantidade.
        var limit = today.AddDays(windowDays);
        var query = dbContext.Lots
            .Where(l => l.TenantId == tenantId
                        && l.Quantity > Estoque.Domain.ValueObjects.Quantity.FromValidated(0m)
                        && l.ExpiresOn <= limit);

        if (branchId is not null)
            query = query.Where(l => l.BranchId == BranchId.From(branchId.Value));

        return query.OrderBy(l => l.ExpiresOn).ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Lot>)t.Result, ct);
    }
}

public sealed class OutletItemRepository(EstoqueDbContext dbContext) : IOutletItemRepository
{
    public async Task AddAsync(OutletItem outlet, CancellationToken ct = default)
        => await dbContext.OutletItems.AddAsync(outlet, ct);

    public Task<OutletItem?> GetOpenAsync(TenantId tenantId, Guid outletItemId, CancellationToken ct = default)
        => dbContext.OutletItems.FirstOrDefaultAsync(o =>
            o.TenantId == tenantId && o.Id == OutletItemId.From(outletItemId), ct);

    public async Task<(IReadOnlyList<OutletItem> Items, int Total)> ListOpenAsync(
        TenantId tenantId, Guid? branchId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.OutletItems
            .Where(o => o.TenantId == tenantId && o.ResolvedAtUtc == null);

        if (branchId is not null)
            query = query.Where(o => o.BranchId == BranchId.From(branchId.Value));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

public sealed class AlertRepository(EstoqueDbContext dbContext) : IAlertRepository
{
    public async Task AddAsync(Alert alert, CancellationToken ct = default)
        => await dbContext.Alerts.AddAsync(alert, ct);

    public Task<bool> ExistsOnDateAsync(TenantId tenantId, AlertType type, Guid? branchId,
        Guid? productId, DateOnly day, CancellationToken ct = default)
    {
        Estoque.Domain.Common.BranchId? branchFilter =
            branchId is null ? null : Estoque.Domain.Common.BranchId.From(branchId.Value);
        ProductId? productFilter =
            productId is null ? null : Estoque.Domain.Common.ProductId.From(productId.Value);

        return dbContext.Alerts.AnyAsync(a =>
            a.TenantId == tenantId
            && a.Type == type
            && a.GeneratedOn == day
            && a.AcknowledgedAtUtc == null
            && a.BranchId == branchFilter
            && a.ProductId == productFilter, ct);
    }

    public Task<Alert?> GetAsync(TenantId tenantId, Guid alertId, CancellationToken ct = default)
        => dbContext.Alerts.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == AlertId.From(alertId), ct);

    public async Task<(IReadOnlyList<Alert> Items, int Total)> ListAsync(
        TenantId tenantId, bool unacknowledgedOnly, AlertType? type, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Alerts.AsNoTracking().Where(a => a.TenantId == tenantId);

        if (unacknowledgedOnly)
            query = query.Where(a => a.AcknowledgedAtUtc == null);
        if (type is not null)
            query = query.Where(a => a.Type == type.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

