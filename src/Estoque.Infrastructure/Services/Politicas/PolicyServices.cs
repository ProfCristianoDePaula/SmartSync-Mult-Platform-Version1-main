using Estoque.Application.Common;
using Estoque.Application.Politicas;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;

namespace Estoque.Infrastructure.Services.Politicas;

public sealed class StockRuleService(IStockRuleRepository rules, IUnitOfWork uow) : IStockRuleService
{
    /// <summary>Upsert: substitui a regra existente de (tenant, produto[, filial]).</summary>
    public async Task<StockRuleDto> SetAsync(SetStockRuleCommand command, CancellationToken ct = default)
    {
        var existing = await rules.FindExactAsync(command.TenantId, command.ProductId, command.BranchId, ct);

        if (existing is not null)
        {
            existing.Update(command.MinimumQuantity, command.ReorderPoint, command.LeadTimeDays);
            await uow.SaveChangesAsync(ct);
            return ToDto(existing);
        }

        var rule = StockRule.Create(command.TenantId, command.ProductId, command.BranchId,
                                    command.MinimumQuantity, command.ReorderPoint, command.LeadTimeDays);
        await rules.AddAsync(rule, ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<PagedResult<StockRuleDto>> ListAsync(TenantId tenantId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (items, total) = await rules.ListAsync(tenantId, page, pageSize, ct);
        return new PagedResult<StockRuleDto>(
            items.Select(ToDto).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    private static StockRuleDto ToDto(StockRule r) => new(
        r.Id.Value, r.ProductId.Value, r.BranchId?.Value,
        r.MinimumQuantity.Value, r.ReorderPoint.Value, r.LeadTimeDays);
}

public sealed class LotService(ILotRepository lots) : ILotService
{
    public async Task<PagedResult<LotDto>> ListExpiringAsync(ListExpiringLotsQuery query, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await lots.ListExpiringAsync(query.TenantId, today, query.Days, query.BranchId, ct);

        var list = items.ToList();
        var page = query.Page;
        var pageSize = query.PageSize;
        var total = list.Count;
        var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<LotDto>(
            paged.Select(ToDto).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    private static LotDto ToDto(Lot l) => new(
        l.Id.Value, l.BranchId.Value, l.ProductId.Value, l.SupplierId?.Value,
        l.Number, l.ExpiresOn, l.Quantity.Value, l.IsActive);
}

public sealed class OutletItemService(
    IOutletItemRepository outlets,
    IAlertRepository alerts,
    IUnitOfWork uow) : IOutletItemService
{
    public async Task<OutletDto> MarkAsync(MarkOutletCommand command, CancellationToken ct = default)
    {
        var outlet = OutletItem.Create(command.TenantId, command.BranchId, command.ProductId,
                                       command.Reason, command.Quantity,
                                       command.SuggestedDiscountPct, command.CreatedByUserId);
        await outlets.AddAsync(outlet, ct);

        // Alerta de outlet (dedupe por dia/referência).
        if (!await alerts.ExistsOnDateAsync(command.TenantId, AlertType.Outlet,
                command.BranchId, command.ProductId, DateOnly.FromDateTime(DateTime.UtcNow), ct))
        {
            await alerts.AddAsync(Alert.Create(
                command.TenantId, command.BranchId, command.ProductId, null,
                AlertType.Outlet, AlertSeverity.Aviso,
                $"Produto marcado como outlet ({command.Reason}) com {command.SuggestedDiscountPct}% de desconto sugerido."), ct);
        }

        await uow.SaveChangesAsync(ct);
        return ToDto(outlet);
    }

    public async Task<bool> ResolveAsync(ResolveOutletCommand command, CancellationToken ct = default)
    {
        var outlet = await outlets.GetOpenAsync(command.TenantId, command.OutletItemId, ct);
        if (outlet is null || outlet.ResolvedAtUtc is not null)
            return false;

        outlet.Resolve();
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<OutletDto>> ListOpenAsync(
        TenantId tenantId, Guid? branchId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (items, total) = await outlets.ListOpenAsync(tenantId, branchId, page, pageSize, ct);
        return new PagedResult<OutletDto>(
            items.Select(ToDto).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    private static OutletDto ToDto(OutletItem o) => new(
        o.Id.Value, o.BranchId.Value, o.ProductId.Value, o.Reason,
        o.SuggestedDiscountPct, o.Quantity.Value, o.CreatedAtUtc, o.ResolvedAtUtc);
}

public sealed class AlertService(IAlertRepository alerts, IUnitOfWork uow) : IAlertService
{
    public async Task<PagedResult<AlertDto>> ListAsync(ListAlertsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await alerts.ListAsync(query.TenantId, query.UnacknowledgedOnly, query.Type, query.Page, query.PageSize, ct);
        return new PagedResult<AlertDto>(
            items.Select(ToDto).ToList(), query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    public async Task<bool> AcknowledgeAsync(AcknowledgeAlertCommand command, CancellationToken ct = default)
    {
        var alert = await alerts.GetAsync(command.TenantId, command.AlertId, ct);
        if (alert is null || alert.AcknowledgedAtUtc is not null)
            return false;

        alert.Acknowledge(command.UserId);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private static AlertDto ToDto(Alert a) => new(
        a.Id.Value, a.BranchId?.Value, a.ProductId?.Value, a.LotIdRef?.Value,
        a.Type, a.Severity, a.Message, a.CreatedAtUtc, a.AcknowledgedAtUtc is not null);
}

