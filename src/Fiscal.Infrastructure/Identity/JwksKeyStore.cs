using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fiscal.Infrastructure.Identity;

/// <summary>
/// Armazenamento em cache da(s) chave(s) públicas do Identity (JWKS).
/// Singleton atualizado por hosted service (refresh periódico) e consultado
/// de forma SÍNCRONA pelo IssuerSigningKeyResolver do JwtBearer — nunca há
/// I/O no caminho da validação depois do primeiro fetch.
/// </summary>
public sealed class JwksKeyStore
{
    private readonly object _lock = new();
    private IReadOnlyList<SecurityKey> _keys = [];

    public IReadOnlyList<SecurityKey> GetKeys() { lock (_lock) return _keys; }

    public async Task RefreshAsync(HttpClient httpClient, CancellationToken ct)
    {
        var json = await httpClient.GetStringAsync("api/auth/jwks", ct);
        var set = new JsonWebKeySet(json);

        if (set.Keys.Count == 0)
            throw new InvalidOperationException("JWKS do Identity vazio (0 chaves).");

        var securityKeys = set.GetSigningKeys();
        if (securityKeys.Count == 0)
            throw new InvalidOperationException("JWKS sem chaves de assinatura válidas.");

        lock (_lock)
            _keys = securityKeys.ToList();
    }
}

/// <summary>Refresh periódico das chaves (hosted service).</summary>
public sealed class JwksRefreshService(
    JwksKeyStore store,
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityClientOptions> options,
    ILogger<JwksRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ttl = TimeSpan.FromMinutes(Math.Max(5, options.Value.JwksCacheMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = httpClientFactory.CreateClient("identity");
                await store.RefreshAsync(client, stoppingToken);
                logger.LogInformation("JWKS do Identity carregado ({Count} chave(s)).",
                    store.GetKeys().Count);
                await Task.Delay(ttl, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao carregar JWKS do Identity; nova tentativa em 15s.");
                try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
