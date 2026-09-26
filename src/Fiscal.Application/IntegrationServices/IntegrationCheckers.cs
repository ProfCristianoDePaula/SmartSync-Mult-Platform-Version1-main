namespace Fiscal.Application.IntegrationServices;

/// <summary>
/// Gate do módulo: verifica se o tenant do token tem o módulo (slug) ativo —
/// chama GET /api/tenants/me/modules do Identity com o TOKEN DO USUÁRIO e
/// cacheia o resultado. Consulta direta ao banco é proibida.
/// </summary>
public interface IModuleAccessChecker
{
    Task<bool> IsModuleActiveAsync(Guid tenantId, string moduleSlug, CancellationToken ct = default);
}
