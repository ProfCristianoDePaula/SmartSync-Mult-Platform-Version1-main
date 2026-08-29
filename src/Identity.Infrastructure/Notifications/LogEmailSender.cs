using Identity.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Notifications;

/// <summary>
/// Fallback de dev (Email:SmtpHost vazio): não envia e-mail de verdade, apenas
/// loga o que seria enviado. Equivale a um "console" do e-mail.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[E-MAIL (dev, não enviado)] Para={To} | Assunto={Subject} | Corpo={Body}",
            to, subject, htmlBody);
        return Task.CompletedTask;
    }
}