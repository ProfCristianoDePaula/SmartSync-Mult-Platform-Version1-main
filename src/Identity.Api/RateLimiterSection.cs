/// <summary>
/// Valores da política de rate limiting (seção "RateLimiting:*" do config).
/// </summary>
public sealed record RateLimiterSection
{
    public int PermitLimit { get; init; }
    public int WindowSeconds { get; init; } = 60;
}