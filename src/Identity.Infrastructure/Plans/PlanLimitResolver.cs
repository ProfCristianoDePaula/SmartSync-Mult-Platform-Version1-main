using Identity.Application.Plans;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Plans;

/// <summary>
/// Implementação do <see cref="IPlanLimitResolver"/>. Soma os limites dos planos
/// dos vínculos ATIVOS do tenant (tenant_modules → plans). Regra decidida pelo
/// usuário: qualquer plano ativo com <c>null</c> ("sem limite") torna o efetivo
/// <c>null</c>; sem vínculo ativo, não há limite contratado → <c>null</c>. Planos
/// soft-deletados ficam fora (o query filter "Active" do Plan os exclui).
/// </summary>
public sealed class PlanLimitResolver : IPlanLimitResolver
{
    private readonly IdentityDbContext _dbContext;

    public PlanLimitResolver(IdentityDbContext dbContext) => _dbContext = dbContext;

    public async Task<PlanLimits> GetEffectiveAsync(Guid tenantId, CancellationToken ct = default)
    {
        var id = TenantId.From(tenantId);

        var plans = await (
            from tm in _dbContext.TenantModules
            join p in _dbContext.Plans on tm.PlanId equals p.Id
            where tm.TenantId == id && tm.Status == TenantModuleStatus.Active
            select p)
            .AsNoTracking()
            .ToListAsync(ct);

        if (plans.Count == 0)
            return new PlanLimits(null, null, null);

        return new PlanLimits(
            SumOrNull(plans, p => p.MaxBranches),
            SumOrNull(plans, p => p.MaxUsers),
            SumOrNull(plans, p => p.MaxStorageMb));
    }

    private static int? SumOrNull(IReadOnlyList<Plan> plans, Func<Plan, int?> selector)
    {
        var sum = 0;
        foreach (var plan in plans)
        {
            var value = selector(plan);
            if (value is null)
                return null;
            sum += value.Value;
        }

        return sum;
    }
}
