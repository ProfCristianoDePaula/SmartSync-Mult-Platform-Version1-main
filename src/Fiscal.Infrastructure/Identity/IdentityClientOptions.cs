namespace Fiscal.Infrastructure.Identity;

/// <summary>
/// Configuração do cliente do microsserviço Identity — seção "Identity".
/// BaseUrl deve apontar para o serviço interno na rede Docker
/// (ex.: http://api:8080).
/// </summary>
public sealed class IdentityClientOptions
{
    public const string SectionName = "Identity";

    /// <summary>URL base do Identity (sem barra final).</summary>
    public string BaseUrl { get; set; } = "http://api:8080";

    /// <summary>TTL do cache da chave pública JWKS.</summary>
    public int JwksCacheMinutes { get; set; } = 720;

    /// <summary>TTL do cache do resultado do gate de módulo por tenant.</summary>
    public int ModuleCacheSeconds { get; set; } = 300;

    /// <summary>TTL do cache da validação de filial por tenant.</summary>
    public int BranchCacheSeconds { get; set; } = 300;

    /// <summary>Slug do módulo deste serviço no catálogo do Identity.</summary>
    public string ModuleSlug { get; set; } = "fiscal";
}
