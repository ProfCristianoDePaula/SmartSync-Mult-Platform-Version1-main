namespace Identity.Application.Auth;

/// <summary>
/// Dados extraídos do IdP externo (Google/Facebook) já normalizados para o
/// processo de primeiro login / login recorrente. O e-mail vindo do IdP é
/// considerado confirmado.
/// </summary>
public sealed record SocialLoginRequest(
    string Provider,
    string ProviderKey,
    string Email,
    string? FullName,
    Guid? TenantId);