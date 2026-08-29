using Estoque.Domain.Common;

namespace Estoque.Application.IntegrationServices;

/// <summary>
/// Resultado da validação de posse de filial.
/// Allowed = filial pertence ao tenant; Denied = não pertence/inexistente;
/// Unknown = não foi possível validar (role sem permissão na API do Identity) —
/// o chamador decide a política de fallback (v1: aceitar escopado por tenant).
/// </summary>
public enum FilialAccess
{
    Allowed,
    Denied,
    Unknown
}

/// <summary>
/// Gate do módulo: verifica se o tenant do token tem o módulo (slug) ativo —
/// chama GET /api/tenants/me/modules do Identity com o TOKEN DO USUÁRIO e
/// cacheia o resultado (contrato §12.2). Consulta direta ao banco é proibida.
/// </summary>
public interface IModuleAccessChecker
{
    Task<bool> IsModuleActiveAsync(Guid tenantId, string moduleSlug, CancellationToken ct = default);
}

/// <summary>
/// Valida que uma filial pertence ao tenant informado via API HTTP do Identity
/// (com cache curto). Implementação em Infrastructure/Identity.
/// </summary>
public interface IFilialAccessChecker
{
    Task<FilialAccess> ValidateAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default);
}
