using Estoque.Application.Common;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using FluentValidation;

namespace Estoque.Application.Politicas;

// ---------------------------------------------------------------------------
// StockRule
// ---------------------------------------------------------------------------

public sealed record SetStockRuleCommand(
    Guid ProductId,
    Guid? BranchId,
    decimal MinimumQuantity,
    decimal ReorderPoint,
    int? LeadTimeDays)
{
    public SetStockRuleCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record StockRuleDto(
    Guid Id,
    Guid ProductId,
    Guid? BranchId,
    decimal MinimumQuantity,
    decimal ReorderPoint,
    int? LeadTimeDays);

public sealed class SetStockRuleCommandValidator : AbstractValidator<SetStockRuleCommand>
{
    public SetStockRuleCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.MinimumQuantity).GreaterThan(0);
        RuleFor(x => x.ReorderPoint).GreaterThanOrEqualTo(x => x.MinimumQuantity)
            .WithMessage("Ponto de pedido não pode ser menor que o estoque mínimo.");
        RuleFor(x => x.LeadTimeDays).InclusiveBetween(0, 365)
            .When(x => x.LeadTimeDays.HasValue);
    }
}

// ---------------------------------------------------------------------------
// Lot
// ---------------------------------------------------------------------------

public sealed record LotDto(
    Guid Id,
    Guid BranchId,
    Guid ProductId,
    Guid? SupplierId,
    string Number,
    DateOnly ExpiresOn,
    decimal Quantity,
    bool IsActive);

public sealed record ListExpiringLotsQuery(int Days = 30, Guid? BranchId = null, int Page = 1, int PageSize = 50)
{
    public ListExpiringLotsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed class ListExpiringLotsQueryValidator : AbstractValidator<ListExpiringLotsQuery>
{
    public ListExpiringLotsQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 365);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// ---------------------------------------------------------------------------
// Outlet
// ---------------------------------------------------------------------------

public sealed record MarkOutletCommand(
    Guid BranchId,
    Guid ProductId,
    OutletReason Reason,
    decimal Quantity,
    int SuggestedDiscountPct)
{
    public MarkOutletCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, CreatedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid CreatedByUserId { get; init; }
}

public sealed record ResolveOutletCommand(Guid OutletItemId)
{
    public ResolveOutletCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, ResolvedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid ResolvedByUserId { get; init; }
}

public sealed record OutletDto(
    Guid Id,
    Guid BranchId,
    Guid ProductId,
    OutletReason Reason,
    int SuggestedDiscountPct,
    decimal Quantity,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);

public sealed class MarkOutletCommandValidator : AbstractValidator<MarkOutletCommand>
{
    public MarkOutletCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.SuggestedDiscountPct).InclusiveBetween(0, 90);
    }
}

// ---------------------------------------------------------------------------
// Alerts
// ---------------------------------------------------------------------------

public sealed record AlertDto(
    Guid Id,
    Guid? BranchId,
    Guid? ProductId,
    Guid? LotId,
    AlertType Type,
    AlertSeverity Severity,
    string Message,
    DateTime CreatedAtUtc,
    bool Acknowledged);

public sealed record ListAlertsQuery(
    bool UnacknowledgedOnly = true,
    AlertType? Type = null,
    int Page = 1,
    int PageSize = 20)
{
    public ListAlertsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record AcknowledgeAlertCommand(Guid AlertId)
{
    public AcknowledgeAlertCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, UserId = userId };
    public TenantId TenantId { get; init; }
    public Guid UserId { get; init; }
}

public sealed class ListAlertsQueryValidator : AbstractValidator<ListAlertsQuery>
{
    public ListAlertsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
