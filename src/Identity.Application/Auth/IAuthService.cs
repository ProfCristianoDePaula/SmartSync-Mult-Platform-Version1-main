namespace Identity.Application.Auth;

/// <summary>
/// Contrato do serviço de autenticação e controle de conta, implementado na
/// Infrastructure. Agrupa login/refresh, confirmação de e-mail/celular (Etapa
/// 06) e gestão de senha/logout para todas as roles (Etapa 11).
/// </summary>
public interface IAuthService
{
    Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cadastro público de Client (autosserviço) vinculado a um tenant. Cria a
    /// conta com e-mail não confirmado, envia o link de confirmação e revoga
    /// sessões (não emite tokens — o login exige e-mail confirmado).
    /// </summary>
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Reenvia o e-mail de confirmação (resposta neutra por segurança).</summary>
    Task ResendConfirmationEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Confirma o e-mail usando o token nativo do Identity.</summary>
    Task<bool> ConfirmEmailAsync(string email, string token, CancellationToken ct = default);

    /// <summary>Gera o código de celular (token nativo) e envia por SMS ao Client autenticado.</summary>
    Task SendSmsCodeAsync(Guid userId, string phoneNumber, CancellationToken ct = default);

    /// <summary>Confirma o celular validando o código SMS e persistindo o número.</summary>
    Task<bool> ConfirmPhoneAsync(Guid userId, string phoneNumber, string code, CancellationToken ct = default);

    // ---- Gestão de senha (Etapa 11) ----

    /// <summary>Gera o token de reset (nativo do Identity) e envia o link por e-mail.</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);

    /// <summary>Gera e envia o código de reset (6 dígitos, 10 min) por SMS.</summary>
    Task ForgotPasswordSmsAsync(string email, CancellationToken ct = default);

    /// <summary>Efetiva o reset: aceita o token do e-mail ou o código de 6 dígitos do SMS.</summary>
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default);

    /// <summary>Troca de senha autenticada (valida a senha atual).</summary>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>
    /// Completa o cadastro do Client autenticado (documento obrigatório; nome
    /// opcional). Em sucesso reemite o par de tokens com a claim
    /// "profile_complete" atualizada e revoga as sessões anteriores.
    /// </summary>
    Task<CompleteProfileResult> CompleteProfileAsync(
        Guid userId,
        CompleteProfileRequest request,
        CancellationToken ct = default);

    /// <summary>Revoga o refresh token informado (logout de uma sessão).</summary>
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct = default);

    /// <summary>Revoga todos os refresh tokens ativos do usuário (logout em todos os dispositivos).</summary>
    Task LogoutAllAsync(Guid userId, CancellationToken ct = default);
}