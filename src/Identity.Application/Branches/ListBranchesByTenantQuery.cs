namespace Identity.Application.Branches;

/// <summary>Lista as filiais de um tenant com paginação. O <see cref="TenantId"/>
/// é sempre imposto pela query — a listagem nunca atravessa tenants.</summary>
public sealed record ListBranchesByTenantQuery(
    Guid TenantId,
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false);
