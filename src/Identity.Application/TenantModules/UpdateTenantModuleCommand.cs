namespace Identity.Application.TenantModules;

/// <summary>
/// Troca de plano em um vínculo tenant↔módulo (upgrade/downgrade, Etapa 15).
/// Se já existe um vínculo ATIVO com o mesmo plano, a operação é idempotente.
/// Se o plano difere, a vigência atual é inativada (preservando histórico) e
/// uma nova é criada. Sem vínculo ativo, cria um novo (reativação).
/// </summary>
public sealed record UpdateTenantModuleCommand(
    Guid TenantId,
    Guid ModuleId,
    Guid PlanId);
