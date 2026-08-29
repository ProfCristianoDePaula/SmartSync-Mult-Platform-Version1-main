using FluentValidation;
using Identity.Application.Common;
using Identity.Application.Modules;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Modules;

/// <summary>
/// Implementação do CRUD de módulos (Etapa 15). Valida com FluentValidation
/// (Application), garante slug E nome únicos entre módulos ATIVOS, converte
/// ArgumentException de validação do slug em 400 e aplica soft delete via
/// named query filter ("Active").
/// </summary>
public sealed class ModuleService : IModuleService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IValidator<CreateModuleCommand> _createValidator;
    private readonly IValidator<UpdateModuleCommand> _updateValidator;

    public ModuleService(
        IdentityDbContext dbContext,
        IValidator<CreateModuleCommand> createValidator,
        IValidator<UpdateModuleCommand> updateValidator)
    {
        _dbContext = dbContext;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ModuleDto> CreateAsync(CreateModuleCommand command, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(command, ct);
        await EnsureSlugUniqueAsync(command.Slug, excludeId: null, ct);
        await EnsureNameUniqueAsync(command.Name, excludeId: null, ct);

        Module module;
        try
        {
            module = Module.Create(command.Name, command.Slug, command.Description);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleViolationException(ex.Message, "module.slug.invalid");
        }

        _dbContext.Modules.Add(module);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(module);
    }

    public async Task<ModuleDto?> UpdateAsync(UpdateModuleCommand command, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(command, ct);

        var module = await _dbContext.Modules
            .FirstOrDefaultAsync(m => m.Id == ModuleId.From(command.Id), ct);
        if (module is null)
            return null;

        await EnsureNameUniqueAsync(command.Name, command.Id, ct);

        module.Update(command.Name, command.Description);
        await _dbContext.SaveChangesAsync(ct);

        return ToDto(module);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteModuleCommand command, CancellationToken ct = default)
    {
        var module = await _dbContext.Modules
            .FirstOrDefaultAsync(m => m.Id == ModuleId.From(command.Id), ct);
        if (module is null)
            return false;

        module.SoftDelete();
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<ModuleDto>> ListAsync(ListModulesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // includeInactive = true quebra o query filter "Active" e lista também
        // os módulos soft-deletados.
        var source = query.IncludeInactive
            ? _dbContext.Modules.IgnoreQueryFilters(["Active"])
            : _dbContext.Modules;

        var total = await source.CountAsync(ct);
        var modules = await source
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ModuleDto>(
            modules.Select(ToDto).ToList(),
            page,
            pageSize,
            total,
            total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ModuleDto?> GetByIdAsync(GetModuleByIdQuery query, CancellationToken ct = default)
    {
        var module = await _dbContext.Modules
            .FirstOrDefaultAsync(m => m.Id == ModuleId.From(query.Id), ct);
        return module is null ? null : ToDto(module);
    }

    private async Task EnsureSlugUniqueAsync(string slug, Guid? excludeId, CancellationToken ct)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var excludeModuleId = excludeId is null ? ModuleId.From(Guid.Empty) : ModuleId.From(excludeId.Value);
        var duplicate = await _dbContext.Modules
            .AnyAsync(m => m.Slug == normalized && m.Id != excludeModuleId, ct);
        if (duplicate)
            throw new BusinessRuleViolationException(
                "Já existe um módulo ativo com este slug.",
                "module.slug.duplicate");
    }

    private async Task EnsureNameUniqueAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToUpperInvariant();
        var excludeModuleId = excludeId is null ? ModuleId.From(Guid.Empty) : ModuleId.From(excludeId.Value);
        var duplicate = await _dbContext.Modules
            .AnyAsync(m => m.Name.ToUpper() == normalized && m.Id != excludeModuleId, ct);
        if (duplicate)
            throw new BusinessRuleViolationException(
                "Já existe um módulo ativo com este nome.",
                "module.name.duplicate");
    }

    private static ModuleDto ToDto(Module module) => new(
        module.Id.Value,
        module.Name,
        module.Slug,
        module.Description,
        module.IsActive,
        module.CreatedAtUtc,
        module.DeletedAtUtc);
}
