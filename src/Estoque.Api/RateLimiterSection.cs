namespace Estoque.Api;

/// <summary>Seção de rate limit (padrão do Identity — Etapa 07).</summary>
public sealed class RateLimiterSection
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; } = 60;
}
