namespace Identity.Application.Auth;

/// <summary>
/// Configurações de emissão de tokens. Vinculada à seção "Jwt" do appsettings.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Nome do emissor (iss).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Público pretendido (aud).</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Vida do access token em minutos.</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>Vida do refresh token em dias.</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    /// <summary>Caminho do PEM com a chave privada (gerada se não existir).</summary>
    public string SigningKeyPath { get; set; } = "keys/jwt-signing-key.pem";

    /// <summary>ID da chave (kid) usado no JWT e no JWKS.</summary>
    public string KeyId { get; set; } = "identity-signing-key";
}