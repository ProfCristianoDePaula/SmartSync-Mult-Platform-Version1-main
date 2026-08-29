using FluentValidation;
using Identity.Application.Common;
using Identity.Application.TenantModules;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.TenantModules;

/// <summary>
/// Implementação dos vínculos tenant↔módulo (Etapa 15). TODAS as operações
/// recebem o TenantId da rota e filtram por ele. Regras centrais:
/// <list type="bullet">
/// <item>no máximo UM vínculo ATIVO por (tenant, module) — garantido pelo
/// índice parcial único no banco e verificado aqui;</item>
/// <item>todo plano referenciado precisa estar ATIVO e pertencer ao MÓDULO do
/// vínculo;</item>
/// <item>troca de plano (PUT) encerra a vigência atual e abre uma nova —
/// preservando histórico para billing; mesmo plano = idempotente;</item>
/// <item>desvincular (DELETE) inativa a vigência atual com EndDateUtc.</item>
/// </list>
/// </summary>
public sealed class TenantModuleService : ITenantModuleService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IValidator<LinkTenantModuleCommand> _linkValidator;
    private readonly IValidator<UpdateTenantModuleCommand> _updateValidator;

    public TenantModuleService(
        IdentityDbContext dbContext,
        IValidator<LinkTenantModuleCommand> linkValidator,
        IValidator<UpdateTenantModuleCommand> updateValidator)
    {
        _dbContext = dbContext;
        _linkValidator = linkValidator;
        _updateValidator = updateValidator;
    }

    public async Task<TenantModuleDto?> LinkAsync(LinkTenantModuleCommand command, CancellationToken ct = default)
    {
        await _linkValidator.ValidateAndThrowAsync(command, ct);

        var tenantId = TenantId.From(command.TenantId);
        var moduleId = ModuleId.From(command.ModuleId);
        var planId = PlanId.From(command.PlanId);

        if (!await TenantActiveAsync(tenantId, ct))
            return null;

        await EnsureModuleActiveAsync(moduleId, ct);
        await EnsurePlanBelongsToModuleAsync(planId, moduleId, ct);

        if (await HasActiveLinkAsync(tenantId, moduleId, ct))
            throw new BusinessRuleViolationException(
                "O tenant já possui um vínculo ativo com este módulo.",
                "tenant_module.active.exists");

        var link = TenantModule.Create(tenantId, moduleId, planId, DateTime.UtcNow);
        _dbContext.TenantModules.Add(link);
        await _dbContext.SaveChangesAsync(ct);

        return await ToDtoAsync(link, ct);
    }

    public async Task<TenantModuleDto?> UpdateAsync(UpdateTenantModuleCommand command, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(command, ct);

        var tenantId = TenantId.From(command.TenantId);
        var moduleId = ModuleId.From(command.ModuleId);
        var planId = PlanId.From(command.PlanId);

        if (!await TenantActiveAsync(tenantId, ct))
            return null;

        await EnsureModuleActiveAsync(moduleId, ct);
        await EnsurePlanBelongsToModuleAsync(planId, moduleId, ct);

        var active = await _dbContext.TenantModules
            .FirstOrDefaultAsync(
                tm => tm.TenantId == tenantId && tm.ModuleId == moduleId && tm.Status == TenantModuleStatus.Active,
                ct);

        // Sem vínculo ativo: cria um novo (reativação do módulo).
        if (active is null)
        {
            var reactivated = TenantModule.Create(tenantId, moduleId, planId, DateTime.UtcNow);
            _dbContext.TenantModules.Add(reactivated);
            await _dbContext.SaveChangesAsync(ct);
            return await ToDtoAsync(reactivated, ct);
        }

        // Mesmo plano: operação idempotente, devolve a vigência atual.
        if (active.PlanId == planId)
            return await ToDtoAsync(active, ct);

        // Plano diferente: encerra a vigência atual (histórico) e abre a nova.
        active.Inactivate(DateTime.UtcNow);
        var upgraded = TenantModule.Create(tenantId, moduleId, planId, DateTime.UtcNow);
        _dbContext.TenantModules.Add(upgraded);
        await _dbContext.SaveChangesAsync(ct);

        return await ToDtoAsync(upgraded, ct);
    }

    public async Task<bool> UnlinkAsync(UnlinkTenantModuleCommand command, CancellationToken ct = default)
    {
        var tenantId = TenantId.From(command.TenantId);
        var moduleId = ModuleId.From(command.ModuleId);

        var active = await _dbContext.TenantModules
            .FirstOrDefaultAsync(
                tm => tm.TenantId == tenantId && tm.ModuleId == moduleId && tm.Status == TenantModuleStatus.Active,
                ct);
        if (active is null)
            return false;

        active.Inactivate(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<TenantModuleDto>> ListAsync(ListTenantModulesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var tenantId = TenantId.From(query.TenantId);

        // O TenantId é sempre imposto: só vínculos DESTE tenant entram na query.
        var source = _dbContext.TenantModules.Where(tm => tm.TenantId == tenantId);

        // Por padrão só a vigência ATIVA; includeInactive=true traz também o
        // histórico (vigências encerradas).
        if (!query.IncludeInactive)
            source = source.Where(tm => tm.Status == TenantModuleStatus.Active);

        var total = await source.CountAsync(ct);
        var links = await source
            .OrderByDescending(tm => tm.StartDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = await ToDtoListAsync(links, ct);

        return new PagedResult<TenantModuleDto>(
            dtos,
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<IReadOnlyList<TenantModuleView>> ListActiveForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var id = TenantId.From(tenantId);

        var links = await _dbContext.TenantModules
            .Where(tm => tm.TenantId == id && tm.Status == TenantModuleStatus.Active)
            .OrderByDescending(tm => tm.StartDateUtc)
            .ToListAsync(ct);

        if (links.Count == 0)
            return [];

        var moduleIds = links.Select(l => l.ModuleId).Distinct().ToList();
        var planIds = links.Select(l => l.PlanId).Distinct().ToList();

        // Só módulos ATIVOS (não soft-deletados) entram na visão do tenant; o
        // IgnoreQueryFilters garante que a exclusão seja explícita (IsActive).
        var modules = await _dbContext.Modules
            .IgnoreQueryFilters(["Active"])
            .Where(m => m.IsActive && moduleIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var plans = await _dbContext.Plans
            .IgnoreQueryFilters(["Active"])
            .Where(p => planIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        return links
            .Where(l => modules.ContainsKey(l.ModuleId))
            .Select(l => new TenantModuleView(
                modules[l.ModuleId].Slug,
                modules[l.ModuleId].Name,
                plans.TryGetValue(l.PlanId, out var plan) ? plan.Name : l.PlanId.ToString(),
                "active",
                l.StartDateUtc,
                l.EndDateUtc))
            .ToList();
    }

    private Task<bool> TenantActiveAsync(TenantId tenantId, CancellationToken ct)
        => _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, ct);

    private async Task EnsureModuleActiveAsync(ModuleId moduleId, CancellationToken ct)
    {
        // O query filter "Active" do Module já exclui módulos inativos.
        var active = await _dbContext.Modules.AnyAsync(m => m.Id == moduleId, ct);
        if (!active)
            throw new BusinessRuleViolationException(
                "O módulo informado não existe ou está inativo.",
                "tenant_module.module.invalid");
    }

    private async Task EnsurePlanBelongsToModuleAsync(PlanId planId, ModuleId moduleId, CancellationToken ct)
    {
        // O query filter "Active" do Plan já exclui planos inativos; a checagem
        // do ModuleId garante que o plano pertence ao módulo do vínculo.
        var active = await _dbContext.Plans
            .AnyAsync(p => p.Id == planId && p.ModuleId == moduleId, ct);
        if (!active)
            throw new BusinessRuleViolationException(
                "O plano informado não existe, está inativo ou não pertence a este módulo.",
                "tenant_module.plan.invalid");
    }

    private Task<bool> HasActiveLinkAsync(TenantId tenantId, ModuleId moduleId, CancellationToken ct)
        => _dbContext.TenantModules.AnyAsync(
            tm => tm.TenantId == tenantId && tm.ModuleId == moduleId && tm.Status == TenantModuleStatus.Active,
            ct);

    private async Task<TenantModuleDto> ToDtoAsync(TenantModule link, CancellationToken ct)
    {
        var module = await _dbContext.Modules
            .IgnoreQueryFilters(["Active"])
            .FirstOrDefaultAsync(m => m.Id == link.ModuleId, ct);

        return MapDto(link, module);
    }

    private async Task<List<TenantModuleDto>> ToDtoListAsync(List<TenantModule> links, CancellationToken ct)
    {
        if (links.Count == 0)
            return [];

        var moduleIds = links.Select(l => l.ModuleId).Distinct().ToList();

        // IgnoreQueryFilters para os nomes SEMPRE resolverem, mesmo se o módulo
        // for soft-deletado.
        var modules = await _dbContext.Modules
            .IgnoreQueryFilters(["Active"])
            .Where(m => moduleIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        return links.Select(l => MapDto(l, GetOrDefault(modules, l.ModuleId)))
            .ToList();
    }

    private static T? GetOrDefault<TKey, T>(IReadOnlyDictionary<TKey, T> source, TKey key)
        where T : class
        => source.TryGetValue(key, out var value) ? value : null;

    private static TenantModuleDto MapDto(TenantModule link, Module? module) => new(
        link.Id.Value,
        link.TenantId.Value,
        link.ModuleId.Value,
        module?.Name ?? link.ModuleId.ToString(),
        link.PlanId.Value,
        link.Status,
        link.StartDateUtc,
        link.EndDateUtc,
        link.CreatedAtUtc);
}
