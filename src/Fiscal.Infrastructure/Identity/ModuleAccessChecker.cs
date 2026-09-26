using Fiscal.Application.IntegrationServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Identity;

/// <summary>
/// Gate do módulo "fiscal": consulta o Identity (/me/modules) com cache TTL
/// curto (default 5 min). Falha da API do Identity ⇒ fail-CLOSED (403) — não
/// operamos fiscal sem confirmar a contratação (R10, mesmo padrão do Estoque).
/// </summary>
public sealed class ModuleAccessChecker(
    IdentityApiClient apiClient,
    IMemoryCache cache,
    IOptions<IdentityClientOptions> options,
    ILogger<ModuleAccessChecker> logger) : IModuleAccessChecker
{
    public async Task<bool> IsModuleActiveAsync(Guid tenantId, string moduleSlug, CancellationToken ct = default)
    {
        var cacheKey = $"module:{tenantId}:{moduleSlug}";

        if (cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        bool active;
        try
        {
            var modules = await apiClient.GetActiveModulesAsync(ct);
            active = modules.Any(m => m.Slug.Equals(moduleSlug, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao consultar módulos no Identity — fail-closed.");
            active = false;
        }

        cache.Set(cacheKey, active, TimeSpan.FromSeconds(options.Value.ModuleCacheSeconds));
        return active;
    }
}
