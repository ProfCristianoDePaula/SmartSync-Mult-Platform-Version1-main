namespace Identity.Application.Auth;

/// <summary>
/// Resultado de autenticação: access token (JWT) + refresh token (rotativo).
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshToken,
    int RefreshExpiresInSeconds,
    bool RequiresProfileCompletion);