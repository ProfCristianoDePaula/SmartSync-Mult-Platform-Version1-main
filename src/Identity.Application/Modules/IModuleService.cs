using Identity.Application.Common;

namespace Identity.Application.Modules;

/// <summary>
/// Contrato do CRUD de módulos, implementado na Infrastructure. Regras de
/// negócio: slug único global entre módulos ativos e imutável após a criação;
/// nome único entre módulos ativos; soft delete inativa o módulo (que deixa de
/// aparecer nas consultas e de receber novos planos/vínculos).
/// </summary>
public interface IModuleService
{
    Task<ModuleDto> CreateAsync(CreateModuleCommand command, CancellationToken ct = default);
    Task<ModuleDto?> UpdateAsync(UpdateModuleCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteModuleCommand command, CancellationToken ct = default);
    Task<PagedResult<ModuleDto>> ListAsync(ListModulesQuery query, CancellationToken ct = default);
    Task<ModuleDto?> GetByIdAsync(GetModuleByIdQuery query, CancellationToken ct = default);
}
