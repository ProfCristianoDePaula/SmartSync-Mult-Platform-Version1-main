namespace Identity.Application.Observability;

/// <summary>
/// Configuração do cache (Etapa 07).
/// - "Mode": "memory" (IMemoryCache) ou "redis" (IDistributedCache via StackExchange.Redis).
/// - Para produção com múltiplas instâncias/microsserviços, use Redis (ou o fork
///   open source Valkey) — compartilha estado entre instâncias.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public string Mode { get; set; } = "Memory";
    public string RedisConnectionString { get; set; } = string.Empty;

    public bool RedisEnabled =>
        Mode.Equals("redis", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(RedisConnectionString);
}