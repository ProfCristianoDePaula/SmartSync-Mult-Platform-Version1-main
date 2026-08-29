namespace Identity.Application.TenantModules;

/// <summary>
/// Listagem paginada dos vínculos tenant↔módulo de UM tenant (o TenantId vem
/// da rota e é sempre imposto na query). Por padrão lista apenas vínculos
/// ATIVOS; <paramref name="IncludeInactive"/> lista também o histórico
/// (vigências encerradas).
/// </summary>
public sealed record ListTenantModulesQuery(
    Guid TenantId,
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false);
