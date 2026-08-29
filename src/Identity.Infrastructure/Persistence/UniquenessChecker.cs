using Identity.Application.Uniqueness;
using Identity.Domain.Common;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Implementação da checagem de duplicidade com consultas diretas no banco.
/// Executado ANTES de persistir (validadores da Application chamam esta).
/// </summary>
public sealed class UniquenessChecker : IUniquenessChecker
{
    private readonly IdentityDbContext _dbContext;

    public UniquenessChecker(IdentityDbContext dbContext) => _dbContext = dbContext;

    public async Task<bool> IsTenantDocumentTakenAsync(
        Documento documento,
        TenantId? excludeTenantId = null,
        CancellationToken ct = default)
    {
        return await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Documento.Numero == documento.Numero && (excludeTenantId == null || t.Id != excludeTenantId), ct);
    }

    public async Task<bool> IsTenantEmailTakenAsync(
        Email email,
        TenantId? excludeTenantId = null,
        CancellationToken ct = default)
    {
        return await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Email == email && (excludeTenantId == null || t.Id != excludeTenantId), ct);
    }

    public async Task<bool> IsUserEmailTakenAsync(
        TenantId? tenantId,
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default)
    {
        var normalized = email.Trim().ToUpperInvariant();
        var query = _dbContext.Users.AsNoTracking().Where(u => u.NormalizedEmail == normalized);
        if (tenantId is not null)
            query = query.Where(u => u.TenantId == tenantId);
        if (excludeUserId is not null)
            query = query.Where(u => u.Id != excludeUserId);
        return await query.AnyAsync(ct);
    }

    public async Task<bool> IsUserDocumentTakenAsync(
        TenantId? tenantId,
        string document,
        Guid? excludeUserId = null,
        CancellationToken ct = default)
    {
        var digits = new string(document.Where(char.IsDigit).ToArray());
        var query = _dbContext.Users.AsNoTracking().Where(u => u.Document == digits);
        if (tenantId is not null)
            query = query.Where(u => u.TenantId == tenantId);
        if (excludeUserId is not null)
            query = query.Where(u => u.Id != excludeUserId);
        return await query.AnyAsync(ct);
    }
}