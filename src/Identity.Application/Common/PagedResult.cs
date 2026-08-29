namespace Identity.Application.Common;

/// <summary>Resultado paginado genérico usado pelas listagens (planos, tenants).</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
