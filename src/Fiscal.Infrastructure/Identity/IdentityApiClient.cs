using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fiscal.Infrastructure.Identity;

/// <summary>
/// Consultas ao Identity SEMPRE com o token do próprio usuário (forward do
/// header Authorization) — o Identity autoriza por role/claim e devolve apenas
/// o que o usuário tem direito de ver.
/// </summary>
public sealed class IdentityApiClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<IdentityClientOptions> options,
    ILogger<IdentityApiClient> logger)
{
    private sealed record ModuleView(string Module, string Name, string? Plan, string Status);

    public HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("identity");
        client.BaseAddress = new Uri(options.Value.BaseUrl);

        // Forward do token do usuário autenticado.
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(auth))
            client.DefaultRequestHeaders.Authorization =
                AuthenticationHeaderValue.Parse(auth);

        return client;
    }

    /// <summary>Módulos ativos contratados pelo tenant do token (GET /api/tenants/me/modules).</summary>
    public async Task<IReadOnlyList<(string Slug, string Plan)>> GetActiveModulesAsync(CancellationToken ct)
    {
        var client = CreateClient();
        var response = await client.GetAsync("api/tenants/me/modules", ct);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            logger.LogDebug("/me/modules retornou 403 (usuário sem tenant).");
            return [];
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<ModulesResponse>(cancellationToken: ct)
            ?? new ModulesResponse([]);

        return payload.Items
            .Where(m => m.Status?.Equals("active", StringComparison.OrdinalIgnoreCase) ?? true)
            .Select(m => (m.Module, m.Plan ?? string.Empty))
            .ToList();
    }

    private sealed record ModulesResponse(IReadOnlyList<ModuleView> Items);

    /// <summary>Valida posse da filial (GET /api/tenants/{tenant}/branches/{branch}).</summary>
    public async Task<Fiscal.Application.IntegrationServices.FilialAccess> CheckBranchAsync(
        Guid tenantId, Guid branchId, CancellationToken ct)
    {
        var client = CreateClient();
        using var response = await client.GetAsync(
            $"api/tenants/{tenantId}/branches/{branchId}", ct);

        return response.StatusCode switch
        {
            HttpStatusCode.OK => Fiscal.Application.IntegrationServices.FilialAccess.Allowed,
            HttpStatusCode.NotFound => Fiscal.Application.IntegrationServices.FilialAccess.Denied,
            HttpStatusCode.Forbidden => Fiscal.Application.IntegrationServices.FilialAccess.Unknown,
            _ => Fiscal.Application.IntegrationServices.FilialAccess.Unknown
        };
    }
}
