namespace Identity.Application.Plans;

/// <summary>
/// Resolve os limites efetivos do tenant a partir dos planos dos vínculos
/// ATIVOS (tenant_modules). Regra (decisão do usuário): o limite efetivo é a
/// SOMA dos limites de todos os planos ativos; se QUALQUER plano ativo tiver
/// limite <c>null</c> ("sem limite"), o efetivo é <c>null</c> (sem limite).
/// Tenant sem vínculo ativo = sem limite contratado.
/// </summary>
public interface IPlanLimitResolver
{
    Task<PlanLimits> GetEffectiveAsync(Guid tenantId, CancellationToken ct = default);
}
