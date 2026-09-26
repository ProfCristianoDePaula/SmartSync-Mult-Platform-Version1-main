using Fiscal.Application.IntegrationServices;
using Fiscal.Domain.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Identity;

/// <summary>
/// Valida posse da filial via HTTP com cache. Escrita fiscal é fail-closed:
/// Denied/Unknown recusam a operação (ao contrário do Estoque v1, aqui a
/// filial define o estabelecimento — risco alto demais para fallback).
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
        catch
        {
            result = FilialAccess.Unknown;
        }

        cache.Set(cacheKey, result, TimeSpan.FromSeconds(options.Value.BranchCacheSeconds));
        return result;
    }
}
