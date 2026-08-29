using Estoque.Application.Common;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using FluentValidation;

namespace Estoque.Application.AutoCompra;

public sealed record PurchaseSuggestionDto(
    Guid Id,
    Guid ProductId,
    Guid BranchId,
    Guid? SupplierId,
    decimal SuggestedQuantity,
    decimal ObservedBalance,
    PurchaseSuggestionStatus Status,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc);

/// <summary>Decisão sobre uma sugestão aberta: approve=true aprova; false descarta.</summary>
public sealed record DecideSuggestionCommand(Guid SuggestionId, bool Approve)
{
    public DecideSuggestionCommand WithContext(TenantId tenantId, Guid userId)
        => this with { TenantId = tenantId, DecidedByUserId = userId };
    public TenantId TenantId { get; init; }
    public Guid DecidedByUserId { get; init; }
}

public sealed record ListSuggestionsQuery(
    bool OpenOnly = true,
    int Page = 1,
    int PageSize = 20)
{
    public ListSuggestionsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public interface IPurchaseSuggestionService
{
    Task<bool> DecideAsync(DecideSuggestionCommand command, CancellationToken ct = default);
    Task<PagedResult<PurchaseSuggestionDto>> ListAsync(ListSuggestionsQuery query, CancellationToken ct = default);
}

public sealed class DecideSuggestionCommandValidator : AbstractValidator<DecideSuggestionCommand>
{
    public DecideSuggestionCommandValidator()
    {
        RuleFor(x => x.SuggestionId).NotEqual(Guid.Empty);
    }
}

