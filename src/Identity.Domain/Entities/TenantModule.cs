using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Domain.Entities;

/// <summary>
/// TenantModule — vínculo de um Tenant com um Module (Etapa 15). Representa
/// UMA vigência: carrega o Plan escolhido (sempre um plano DAQUELE módulo),
/// o status (ativo/inativo) e as datas de início/fim. Upgrade/downgrade e o
/// encerramento de acesso NÃO editam a vigência: inativam a atual
/// (<see cref="Inactivate"/>) e criam uma nova — preservando histórico para
/// billing. Regra: no máximo UM vínculo ATIVO por (Tenant, Module).
/// </summary>
public sealed class TenantModule : Entity<TenantModuleId>
{
    public TenantId TenantId { get; private set; }
    public ModuleId ModuleId { get; private set; }
    public PlanId PlanId { get; private set; }
    public TenantModuleStatus Status { get; private set; }
    public DateTime StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private TenantModule() { }

    private TenantModule(
        TenantModuleId id,
        TenantId tenantId,
        ModuleId moduleId,
        PlanId planId,
        DateTime startDateUtc)
        : base(id)
    {
        TenantId = tenantId;
        ModuleId = moduleId;
        SetPlan(planId);
        Status = TenantModuleStatus.Active;
        StartDateUtc = startDateUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static TenantModule Create(
        TenantId tenantId,
        ModuleId moduleId,
        PlanId planId,
        DateTime startDateUtc)
        => new(TenantModuleId.New(), tenantId, moduleId, planId, startDateUtc);

    public void SetPlan(PlanId planId)
        => PlanId = planId == default ? throw new ArgumentException("Plano é obrigatório.", nameof(planId)) : planId;

    /// <summary>Encerra a vigência atual (status inativo + data de fim).</summary>
    public void Inactivate(DateTime endUtc)
    {
        if (Status == TenantModuleStatus.Inactive)
            return;

        Status = TenantModuleStatus.Inactive;
        EndDateUtc = endUtc;
    }
}
