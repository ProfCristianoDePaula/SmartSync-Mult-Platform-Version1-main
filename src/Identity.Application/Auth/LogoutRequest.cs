namespace Identity.Application.Auth;

/// <summary>
/// Logout de uma sessão específica: revoga o refresh token informado.
/// O access token JWT em si não é invalidado (stateless); a invalidação real
/// ocorre no lado do refresh token armazenado em banco.
/// </summary>
public sealed record LogoutRequest(string RefreshToken);