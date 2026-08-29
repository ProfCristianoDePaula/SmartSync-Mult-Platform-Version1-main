namespace Identity.Application.TenantModules;

/// <summary>
/// Resposta do GET /api/tenants/me/modules — lista de vínculos ATIVOS do tenant
/// do token (apenas módulos não soft-deletados). Formato do contrato §12.2.
/// </summary>
public sealed record TenantModulesView(
    IReadOnlyList<TenantModuleView> Items);
