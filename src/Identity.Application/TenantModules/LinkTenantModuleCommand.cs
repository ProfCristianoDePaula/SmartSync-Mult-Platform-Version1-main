namespace Identity.Application.TenantModules;

/// <summary>
/// Dados para vincular um tenant a um módulo (Etapa 15). O TenantId vem da
/// rota (<c>api/tenants/{tenantId}/modules</c>). Falha com 400 se já existir
/// um vínculo ATIVO do tenant com o módulo (regra: um por vigência).
/// </summary>
public sealed record LinkTenantModuleCommand(
    Guid TenantId,
    Guid ModuleId,
    Guid PlanId);
