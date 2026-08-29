namespace Identity.Application.Notifications;

/// <summary>
/// Envio de e-mail transacional. Implementado na Infrastructure com MailKit
/// (SMTP). A implementação pode alternar entre SMTP real, sandbox (Mailtrap/
/// smtp4dev) ou somente-log, conforme a configuração.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}