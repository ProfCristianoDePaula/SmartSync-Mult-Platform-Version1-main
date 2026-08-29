using Estoque.Application.Common;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using FluentValidation;

namespace Estoque.Application.Movimentacoes;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record StockMovementDto(
    Guid Id,
    Guid BranchId,
    Guid ProductId,
    MovementType Type,
    decimal Quantity,
    decimal? UnitCost,
    Guid? LotId,
    string? OriginDocumentRef,
    Guid PerformedByUserId,
    Guid? CounterpartyBranchId,
    DateTime CreatedAtUtc);

public sealed record StockBalanceDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    Guid BranchId,
    decimal Quantity,
    decimal? AverageUnitCost,
    DateTime UpdatedAtUtc);

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

public sealed record RegisterStockInCommand(
    Guid BranchId,
    Guid ProductId,
    decimal Quantity,
    decimal? UnitCost,
    Guid? LotId,
    Guid? SupplierId,
    string? LotNumber,
    DateOnly? LotExpiresOn,
    string? OriginDocumentRef)
{
    public RegisterStockInCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, PerformedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid PerformedByUserId { get; init; }
}

public sealed record RegisterStockOutCommand(
    Guid BranchId,
    Guid ProductId,
    decimal Quantity,
    Guid? LotId,
    string? OriginDocumentRef)
{
    public RegisterStockOutCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, PerformedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid PerformedByUserId { get; init; }
}

public sealed record AdjustStockCommand(
    Guid BranchId,
    Guid ProductId,
    /// <summary>Delta — pode ser negativo (contagem de inventário).</summary>
    decimal DeltaQuantity,
    string Reason)
{
    public AdjustStockCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, PerformedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid PerformedByUserId { get; init; }
}

public sealed record TransferStockCommand(
    Guid FromBranchId,
    Guid ToBranchId,
    Guid ProductId,
    decimal Quantity)
{
    public TransferStockCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, PerformedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid PerformedByUserId { get; init; }
}

public sealed record ListMovementsQuery(
    Guid? BranchId = null,
    Guid? ProductId = null,
    MovementType? Type = null,
    int Page = 1,
    int PageSize = 20)
{
    public ListMovementsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListBalancesQuery(
    Guid? BranchId = null,
    bool BelowMinimumOnly = false,
    int Page = 1,
    int PageSize = 20)
{
    public ListBalancesQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class RegisterStockInCommandValidator : AbstractValidator<RegisterStockInCommand>
{
    public RegisterStockInCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEqual(Guid.Empty).WithMessage("Filial é obrigatória.");
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0)
            .When(x => x.UnitCost.HasValue);
        // Lote novo exige número + validade
        RuleFor(x => x.LotNumber).NotEmpty()
            .When(x => x.LotExpiresOn.HasValue && x.LotId is null);
        RuleFor(x => x.LotExpiresOn).NotNull()
            .When(x => !string.IsNullOrWhiteSpace(x.LotNumber) && x.LotId is null)
            .WithMessage("Informe a validade do lote.");
    }
}

public sealed class RegisterStockOutCommandValidator : AbstractValidator<RegisterStockOutCommand>
{
    public RegisterStockOutCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.DeltaQuantity).NotEqual(0)
            .WithMessage("O ajuste deve ser diferente de zero.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500)
            .WithMessage("Motivo do ajuste é obrigatório (auditoria).");
    }
}

public sealed class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(x => x.FromBranchId).NotEqual(Guid.Empty);
        RuleFor(x => x.ToBranchId).NotEqual(Guid.Empty)
            .WithMessage("Filial de destino é obrigatória.");
        RuleFor(x => x.ToBranchId).NotEqual(x => x.FromBranchId)
            .WithMessage("Origem e destino devem ser filiais diferentes.");
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class ListMovementsQueryValidator : AbstractValidator<ListMovementsQuery>
{
    public ListMovementsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class ListBalancesQueryValidator : AbstractValidator<ListBalancesQuery>
{
    public ListBalancesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
