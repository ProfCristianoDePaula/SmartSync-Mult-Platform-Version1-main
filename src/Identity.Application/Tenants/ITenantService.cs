using Identity.Application.Common;

namespace Identity.Application.Tenants;

/// <summary>
/// Contrato do CRUD de tenants, implementado na Infrastructure.
/// Regras de negócio: CNPJ e e-mail globalmente únicos (Etapa 04); plano
/// opcional mas, quando informado, deve referenciar um plano ATIVO; soft delete
/// inativa o tenant (que deixa de aparecer nas consultas e cujos usuários não
/// podem mais autenticar).
/// </summary>
public interface ITenantService
{
    Task<TenantDto> CreateAsync(CreateTenantCommand command, CancellationToken ct = default);
    Task<TenantDto?> UpdateAsync(UpdateTenantCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteTenantCommand command, CancellationToken ct = default);
    Task<PagedResult<TenantDto>> ListAsync(ListTenantsQuery query, CancellationToken ct = default);
    Task<TenantDto?> GetByIdAsync(GetTenantByIdQuery query, CancellationToken ct = default);
}
