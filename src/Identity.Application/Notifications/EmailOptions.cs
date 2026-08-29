namespace Identity.Application.Notifications;

/// <summary>
/// Configuração de e-mail transacional. Também serve de "sandbox" para dev:
/// - host/porta apontando para smtp4dev ('127.0.0.1:25') exibe no painel o e-mail;
/// - ou para Mailtrap (credenciais do sandbox);
/// - se o host estiver vazio, usa a implementação LogEmailSender (somente log).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string From { get; set; } = "nao-responda@identity.local";
    public string FromName { get; set; } = "Identity Service";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSmtp => !string.IsNullOrWhiteSpace(SmtpHost);

    /// <summary>
    /// Template do link de confirmação enviado por e-mail. Placeholders:
    /// {email} e {token}. Aponta para o front-end/browser que então chama
    /// POST /api/auth/confirm-email.
    /// </summary>
    public string ConfirmationUrlTemplate { get; set; } =
        "http://localhost:3000/confirm-email?email={email}&token={token}";

    /// <summary>
    /// Template do link de reset de senha enviado por e-mail. Placeholders:
    /// {email} e {token}. Aponta para o front-end/browser que então chama
    /// POST /api/auth/reset-password.
    /// </summary>
    public string PasswordResetUrlTemplate { get; set; } =
        "http://localhost:3000/reset-password?email={email}&token={token}";
}