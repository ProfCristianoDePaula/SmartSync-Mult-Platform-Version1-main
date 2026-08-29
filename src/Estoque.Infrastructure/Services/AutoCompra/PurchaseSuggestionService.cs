using Estoque.Application.AutoCompra;
using Estoque.Application.Common;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Infrastructure.Persistence.Outbox;

namespace Estoque.Infrastructure.Services.AutoCompra;

public sealed class PurchaseSuggestionService(
    IPurchaseSuggestionRepository suggestions,
    IUnitOfWork uow,
    OutboxService outbox) : IPurchaseSuggestionService
{
    public async Task<bool> DecideAsync(DecideSuggestionCommand command, CancellationToken ct = default)
    {
        var suggestion = await suggestions.GetAsync(command.TenantId, command.SuggestionId, ct);
        if (suggestion is null)
            return false;

        if (command.Approve)
            suggestion.Approve(command.DecidedByUserId);
        else
            suggestion.Discard(command.DecidedByUserId);

        await outbox.WriteAsync(suggestion.PopEvents(), ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<PurchaseSuggestionDto>> ListAsync(ListSuggestionsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await suggestions.ListAsync(query.TenantId, query.OpenOnly, query.Page, query.PageSize, ct);
        return new PagedResult<PurchaseSuggestionDto>(
            items.Select(ToDto).ToList(), query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private static PurchaseSuggestionDto ToDto(Domain.Entities.PurchaseSuggestion s) => new(
        s.Id.Value, s.ProductId.Value, s.BranchId.Value, s.SupplierId?.Value,
        s.SuggestedQuantity.Value, s.ObservedBalance.Value, s.Status,
        s.CreatedAtUtc, s.DecidedAtUtc);
}
