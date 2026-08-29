using Identity.Domain.Enums;

namespace Identity.Application.Tenants;

/// <summary>
/// Listagem paginada de tenants com filtros opcionais: status, nome (busca em
/// razão social/nome fantasia) e documento (CPF/CNPJ). Por padrão exclui
/// soft-deletados (query filter); <paramref name="IncludeInactive"/> usa
/// <c>IgnoreQueryFilters("Active")</c>.
/// </summary>
public sealed record ListTenantsQuery(
    int Page = 1,
    int PageSize = 20,
    TenantStatus? Status = null,
    string? Search = null,
    string? Documento = null,
    bool IncludeInactive = false);
