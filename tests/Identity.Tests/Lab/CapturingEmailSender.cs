using Identity.Application.Notifications;

namespace Identity.Tests.Lab;

/// <summary>
/// E-mail capturado pelo <see cref="CapturingEmailSender"/> para permitir
/// testes end-to-end do fluxo de confirmação sem enviar e-mail real.
/// </summary>
public sealed record CapturedEmail(string To, string Subject, string Body);

/// <summary>
/// Substituto de <see cref="IEmailSender"/> que apenas guarda as mensagens em
/// memória (sem SMTP). Registrado no WebApplicationFactory da suite.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public IReadOnlyList<CapturedEmail> Messages => _messages;

    private readonly List<CapturedEmail> _messages = [];

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _messages.Add(new CapturedEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public CapturedEmail? Latest => _messages.LastOrDefault();

    public void Clear() => _messages.Clear();
}