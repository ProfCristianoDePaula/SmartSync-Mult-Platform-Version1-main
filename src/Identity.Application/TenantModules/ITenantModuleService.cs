using Identity.Application.Common;

namespace Identity.Application.TenantModules;

/// <summary>
/// Contrato dos vínculos tenant↔módulo (Etapa 15), implementado na
/// Infrastructure. TODAS as operações recebem o TenantId de forma explícita e
/// filtram por ele. Regras centrais: no máximo UM vínculo ATIVO por
/// (tenant, module); troca de plano encerra a vigência atual e abre uma nova
/// (preservando histórico); todo plano referenciado precisa ser ATIVO e do
/// MÓDULO do vínculo.
/// </summary>
public interface ITenantModuleService
{
    Task<TenantModuleDto?> LinkAsync(LinkTenantModuleCommand command, CancellationToken ct = default);
    Task<TenantModuleDto?> UpdateAsync(UpdateTenantModuleCommand command, CancellationToken ct = default);
    Task<bool> UnlinkAsync(UnlinkTenantModuleCommand command, CancellationToken ct = default);
    Task<PagedResult<TenantModuleDto>> ListAsync(ListTenantModulesQuery query, CancellationToken ct = default);

    /// <summary>
    /// Lista os vínculos ATIVOS de um tenant com o slug/nome do módulo e o nome
    /// do plano (visão do próprio tenant — GET /api/tenants/me/modules, contrato
    /// §12.2). Módulos soft-deletados são excluídos. O TenantId sempre vem
    /// explícito (na prática derivado da claim tenant_id pelo controller).
    /// </summary>
    Task<IReadOnlyList<TenantModuleView>> ListActiveForTenantAsync(Guid tenantId, CancellationToken ct = default);
}
