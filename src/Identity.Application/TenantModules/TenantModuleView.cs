namespace Identity.Application.TenantModules;

/// <summary>
/// Visão de UM vínculo ativo para o próprio tenant (GET /api/tenants/me/modules
/// — contrato §12.2 do CONTRATO-IDENTIDADE). O consumidor usa o slug do módulo
/// (identificador estável) para ligar/desligar features e o nome do plano para
/// aplicar limites (maxBranches/maxUsers/maxStorageMb). <c>Status</c> é sempre
/// <c>"active"</c> (a visão só expõe vínculos ativos).
/// </summary>
public sealed record TenantModuleView(
    string Module,
    string Name,
    string Plan,
    string Status,
    DateTime StartDateUtc,
    DateTime? EndDateUtc);
