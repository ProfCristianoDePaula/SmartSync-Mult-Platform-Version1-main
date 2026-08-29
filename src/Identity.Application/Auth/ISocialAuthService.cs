namespace Identity.Application.Auth;

/// <summary>
/// Contrato do login com provedores sociais (Google/Facebook), restrito à role
/// Client. Trata o primeiro login (cria o Client) e o login recorrente
/// (vincula ao Client existente via AspNetUserLogins).
/// </summary>
public interface ISocialAuthService
{
    /// <summary>
    /// Processa a autenticação social. O resultado é tipado: tokens em caso de
    /// sucesso, ou um <see cref="SocialAuthError"/> que o controller traduz em
    /// HTTP (400/401/403) com mensagem específica.
    /// </summary>
    Task<SocialAuthResult> LoginAsync(SocialLoginRequest request, CancellationToken ct = default);

    /// <summary>Provedores suportados (nomes de scheme).</summary>
    IReadOnlyList<string> SupportedProviders { get; }
}
