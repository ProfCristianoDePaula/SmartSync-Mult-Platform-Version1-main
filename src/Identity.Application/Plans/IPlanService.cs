using Identity.Application.Common;

namespace Identity.Application.Plans;

/// <summary>
/// Contrato do CRUD de planos de assinatura por MÓDULO (Etapa 15), implementado
/// na Infrastructure. Todas as operações recebem o ModuleId da rota e filtram
/// por ele. Regras de negócio: nome único por módulo entre planos ativos;
/// soft delete inativa o plano (que deixa de aparecer nas consultas e de poder
/// ser vinculado a novos Tenants).
/// </summary>
public interface IPlanService
{
    Task<PlanDto?> CreateAsync(CreatePlanCommand command, CancellationToken ct = default);
    Task<PlanDto?> UpdateAsync(UpdatePlanCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeletePlanCommand command, CancellationToken ct = default);
    Task<PagedResult<PlanDto>> ListAsync(ListPlansQuery query, CancellationToken ct = default);
    Task<PlanDto?> GetByIdAsync(GetPlanByIdQuery query, CancellationToken ct = default);
}
