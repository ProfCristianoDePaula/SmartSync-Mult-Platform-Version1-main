namespace Identity.Application.Modules;

/// <summary>
/// Listagem paginada de módulos. Por padrão exclui inativos (query filter);
/// <paramref name="IncludeInactive"/> usa <c>IgnoreQueryFilters("Active")</c>.
/// </summary>
public sealed record ListModulesQuery(
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false);
