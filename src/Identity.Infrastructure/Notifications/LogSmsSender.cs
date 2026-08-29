using Identity.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Notifications;

/// <summary>
/// Fallback de dev (Sms:Provider "log" ou "log"): não envia SMS de verdade,
/// apenas loga o código que seria enviado. Ideal para smtp4dev-equivalente de
/// SMS e para testes sem custo.
/// </summary>
public sealed class LogSmsSender : ISmsSender
{
    private readonly ILogger<LogSmsSender> _logger;

    public LogSmsSender(ILogger<LogSmsSender> logger) => _logger = logger;

    public Task SendAsync(string to, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[SMS (dev, não enviado)] Para={To} | Mensagem={Message}",
            to, message);
        return Task.CompletedTask;
    }
}