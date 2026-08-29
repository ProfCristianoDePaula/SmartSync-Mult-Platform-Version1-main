using Identity.Application.Notifications;

namespace Identity.Tests.Lab;

/// <summary>
/// Mensagem SMS capturada pelo <see cref="CapturingSmsSender"/> (nunca envia
/// SMS de verdade) — permite validar o fluxo de código SMS nos testes.
/// </summary>
public sealed record CapturedSms(string To, string Message);

/// <summary>
/// Substituto de <see cref="ISmsSender"/> que guarda as mensagens em memória.
/// </summary>
public sealed class CapturingSmsSender : ISmsSender
{
    public IReadOnlyList<CapturedSms> Messages => _messages;

    private readonly List<CapturedSms> _messages = [];

    public Task SendAsync(string to, string message, CancellationToken ct = default)
    {
        _messages.Add(new CapturedSms(to, message));
        return Task.CompletedTask;
    }

    public CapturedSms? Latest => _messages.LastOrDefault();

    public void Clear() => _messages.Clear();
}