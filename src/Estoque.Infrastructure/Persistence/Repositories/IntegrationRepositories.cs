using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence.Repositories;

public sealed class PurchaseSuggestionRepository(EstoqueDbContext dbContext) : IPurchaseSuggestionRepository
{
    public async Task AddAsync(PurchaseSuggestion suggestion, CancellationToken ct = default)
        => await dbContext.PurchaseSuggestions.AddAsync(suggestion, ct);

    public Task<PurchaseSuggestion?> GetAsync(TenantId tenantId, Guid suggestionId, CancellationToken ct = default)
        => dbContext.PurchaseSuggestions.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Id == PurchaseSuggestionId.From(suggestionId), ct);

    public Task<bool> HasOpenAsync(TenantId tenantId, ProductId productId, BranchId branchId, CancellationToken ct = default)
        => dbContext.PurchaseSuggestions.AnyAsync(s =>
            s.TenantId == tenantId
            && s.ProductId == productId
            && s.BranchId == branchId
            && s.Status == Domain.Enums.PurchaseSuggestionStatus.Aberta);

    public async Task<(IReadOnlyList<PurchaseSuggestion> Items, int Total)> ListAsync(
        TenantId tenantId, bool openOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.PurchaseSuggestions.AsNoTracking().Where(s => s.TenantId == tenantId);

        if (openOnly)
            query = query.Where(s => s.Status == Domain.Enums.PurchaseSuggestionStatus.Aberta);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}

public sealed class XmlImportRepository(EstoqueDbContext dbContext) : IXmlImportRepository
{
    public async Task AddAsync(XmlImport import, CancellationToken ct = default)
        => await dbContext.XmlImports.AddAsync(import, ct);

    public Task<XmlImport?> GetAsync(TenantId tenantId, Guid importId, CancellationToken ct = default)
        => dbContext.XmlImports.FirstOrDefaultAsync(
            i => i.TenantId == tenantId && i.Id == XmlImportId.From(importId), ct);

    /// <summary>Dequeue FIFO da fila de processamento (worker).</summary>
    public Task<XmlImport?> GetNextPendingAsync(CancellationToken ct = default)
        => dbContext.XmlImports
            .Where(i => i.Status == Domain.Enums.XmlImportStatus.Recebida)
            .OrderBy(i => i.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
}
