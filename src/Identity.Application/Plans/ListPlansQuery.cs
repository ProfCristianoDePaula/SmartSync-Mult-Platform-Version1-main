namespace Identity.Application.Plans;

/// <summary>
/// Listagem paginada dos planos DE UM módulo (o ModuleId vem da rota). Por
/// padrão exclui inativos (query filter); <paramref name="IncludeInactive"/>
/// usa <c>IgnoreQueryFilters("Active")</c>.
/// </summary>
public sealed record ListPlansQuery(
    Guid ModuleId,
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false);
