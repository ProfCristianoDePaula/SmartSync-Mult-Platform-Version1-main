using Identity.Application.Notifications;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Identity.Infrastructure.Notifications;

/// <summary>
/// Envio de e-mail transacional via SMTP usando MailKit. Em dev, o host pode
/// apontar para um SMTP de sandbox gratuito: Mailtrap (smtp.mailtrap.io) ou
/// local (smtp4dev/Papercut — ex.: 127.0.0.1:25).
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, _options.UseSsl, ct);

        if (!string.IsNullOrWhiteSpace(_options.Username))
            await client.AuthenticateAsync(
                _options.Username,
                _options.Password ?? string.Empty,
                ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation(
            "E-mail enviado com sucesso via {Host}:{Port} para {To}",
            _options.SmtpHost, _options.SmtpPort, to);
    }
}