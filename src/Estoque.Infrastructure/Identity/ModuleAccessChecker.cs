using Estoque.Application.IntegrationServices;
using Estoque.Domain.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Estoque.Infrastructure.Identity;

/// <summary>
/// Gate do módulo "estoque": consulta o Identity (/me/modules) com cache TTL
/// curto (default 5 min). Falha da API do Identity ⇒ fail-CLOSED (403) — não
/// operamos estoque sem confirmar a contratação (decisão de design Etapa 23).
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

/// <summary>
/// Valida posse da filial via HTTP com cache. Resultado Unknown (role sem
/// permissão no endpoint do Identity) é devolvido ao chamador para aplicar a
/// política de fallback documentada (v1: aceitar escopado por tenant).
/// </summary>
public sealed class FilialAccessChecker(
    IdentityApiClient apiClient,
    IMemoryCache cache,
    IOptions<IdentityClientOptions> options) : IFilialAccessChecker
{
    public async Task<FilialAccess> ValidateAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default)
    {
        var cacheKey = $"branch:{tenantId}:{branchId}";

        if (cache.TryGetValue(cacheKey, out FilialAccess cached))
            return cached;

        FilialAccess result;
        try
        {
            result = await apiClient.CheckBranchAsync(tenantId.Value, branchId, ct);
        }
        catch (Exception)
        {
            result = FilialAccess.Unknown;
        }

        // Denied é cacheado por mais tempo; Unknown/Allowed com TTL normal.
        var ttl = result == FilialAccess.Denied
            ? TimeSpan.FromMinutes(10)
            : TimeSpan.FromSeconds(options.Value.BranchCacheSeconds);

        cache.Set(cacheKey, result, ttl);
        return result;
    }
}
