using FluentValidation;
using Identity.Application.Common;
using Identity.Application.Plans;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Plans;

/// <summary>
/// Implementação do CRUD de planos por módulo (Etapa 15). TODAS as operações
/// recebem o ModuleId da rota e filtram por ele — a query nunca atravessa
/// módulos. Valida com FluentValidation (Application), garante nome único POR
/// MÓDULO entre planos ATIVOS e aplica soft delete via query filter ("Active").
/// </summary>
public sealed class PlanService : IPlanService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IValidator<CreatePlanCommand> _createValidator;
    private readonly IValidator<UpdatePlanCommand> _updateValidator;

    public PlanService(
        IdentityDbContext dbContext,
        IValidator<CreatePlanCommand> createValidator,
        IValidator<UpdatePlanCommand> updateValidator)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PlanDto?> CreateAsync(CreatePlanCommand command, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(command, ct);

        var moduleId = ModuleId.From(command.ModuleId);

        // O módulo precisa existir e estar ATIVO (o query filter "Active" oculta
        // módulos soft-deletados). Sem isso, retorna null → 404 na API.
        var moduleExists = await _dbContext.Modules.AnyAsync(m => m.Id == moduleId, ct);
        if (!moduleExists)
            return null;

        await EnsureNameUniqueAsync(moduleId, command.Name, excludeId: null, ct);

        var plan = Plan.Create(
            moduleId,
            command.Name,
            command.Description,
            command.MonthlyPrice,
            command.AnnualPrice,
            command.TrialDays,
            command.Features,
            command.MaxBranches,
            command.MaxUsers,
            command.MaxStorageMb);

        _dbContext.Plans.Add(plan);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(plan);
    }

    public async Task<PlanDto?> UpdateAsync(UpdatePlanCommand command, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(command, ct);

        var plan = await FindActiveAsync(command.ModuleId, command.Id, ct);
        if (plan is null)
            return null;

        await EnsureNameUniqueAsync(ModuleId.From(command.ModuleId), command.Name, command.Id, ct);

        plan.Update(
            command.Name,
            command.Description,
            command.MonthlyPrice,
            command.AnnualPrice,
            command.TrialDays,
            command.Features,
            command.MaxBranches,
            command.MaxUsers,
            command.MaxStorageMb);

        await _dbContext.SaveChangesAsync(ct);

        return ToDto(plan);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeletePlanCommand command, CancellationToken ct = default)
    {
        var plan = await FindActiveAsync(command.ModuleId, command.Id, ct);
        if (plan is null)
            return false;

        plan.SoftDelete();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<PlanDto>> ListAsync(ListPlansQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var moduleId = ModuleId.From(query.ModuleId);

        // includeInactive = true quebra o query filter "Active" e lista também
        // os planos soft-deletados.
        var source = query.IncludeInactive
            ? _dbContext.Plans.IgnoreQueryFilters(["Active"])
            : _dbContext.Plans;

        // O ModuleId é sempre imposto: só planos DESTE módulo entram na query.
        source = source.Where(p => p.ModuleId == moduleId);

        var total = await source.CountAsync(ct);
        var plans = await source
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PlanDto>(
            plans.Select(ToDto).ToList(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<PlanDto?> GetByIdAsync(GetPlanByIdQuery query, CancellationToken ct = default)
    {
        var plan = await FindActiveAsync(query.ModuleId, query.Id, ct);
        return plan is null ? null : ToDto(plan);
    }

    /// <summary>Busca um plano ATIVO escopado ao módulo (query filter "Active").
    /// Retorna null se o plano não existir, estiver inativo ou pertencer a outro
    /// módulo.</summary>
    private Task<Plan?> FindActiveAsync(Guid moduleId, Guid planId, CancellationToken ct)
        => _dbContext.Plans
            .FirstOrDefaultAsync(
                p => p.ModuleId == ModuleId.From(moduleId) && p.Id == PlanId.From(planId),
                ct);

    private async Task EnsureNameUniqueAsync(ModuleId moduleId, string name, Guid? excludeId, CancellationToken ct)
    {
        // Unicidade por MÓDULO (Etapa 15): o mesmo nome pode existir em módulos
        // diferentes. O query filter "Active" já exclui planos inativos — vale
        // apenas entre ativos. Comparação case-insensitive via upper() no banco.
        var normalized = name.Trim().ToUpperInvariant();
        var excludePlanId = excludeId is null ? PlanId.From(Guid.Empty) : PlanId.From(excludeId.Value);
        var duplicate = await _dbContext.Plans
            .AnyAsync(
                p => p.ModuleId == moduleId
                     && p.Name.ToUpper() == normalized
                     && p.Id != excludePlanId,
                ct);
        if (duplicate)
            throw new BusinessRuleViolationException(
                "Já existe um plano ativo com este nome neste módulo.",
                "plan.name.duplicate");
    }

    private static PlanDto ToDto(Plan plan) => new(
        plan.Id.Value,
        plan.ModuleId.Value,
        plan.Name,
        plan.Description,
        plan.MonthlyPrice,
        plan.AnnualPrice,
        plan.TrialDays,
        plan.Features.ToList(),
        plan.MaxBranches,
        plan.MaxUsers,
        plan.MaxStorageMb,
        plan.IsActive,
        plan.CreatedAtUtc,
        plan.DeletedAtUtc);
}
