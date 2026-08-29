using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Identity.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Notifications;

/// <summary>
/// Envio de SMS via Twilio REST API (conta de TRIAL gratuito).
///
/// LIMITAÇÃO DOCUMENTADA: não existe SMS transacional ilimitado e gratuito em
/// produção. O trial da Twilio:
/// - envia APENAS para números verificados na conta;
/// - adiciona ao texto uma marca d'água ("Sent from your Twilio trial account");
/// - **só aceita templates predefinidos** (erro 572006 para texto livre) — o
///   campo <c>Body</c> passa a ser o NOME do template (ex.: "sms_2fa") e o
///   conteúdo é genérico da Twilio (sem o código gerado);
/// - tem créditos limitados e expira.
///
/// Modo: <see cref="SmsOptions.BodyTemplate"/> preenchido = trial (template);
/// vazio = texto customizado (conta paga/upgraded).
/// Para dev sem conta, o DI usa <see cref="LogSmsSender"/> (Provider: "log").
///
/// Usa a REST API diretamente com HttpClient (sem o SDK NuGet pesado),
/// mantendo a dependência mínima.
/// </summary>
public sealed class TwilioSmsSender : ISmsSender
{
    private readonly SmsOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(
        IOptions<SmsOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<TwilioSmsSender> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(string to, string message, CancellationToken ct = default)
    {
        if (!_options.TwilioConfigured)
            throw new InvalidOperationException("Twilio não configurado (Sms:AccountSid/AuthToken/FromNumber).");

        var client = _httpClientFactory.CreateClient(nameof(TwilioSmsSender));
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("To", Normalize(to)),
            new KeyValuePair<string, string>("From", _options.FromNumber),
            new KeyValuePair<string, string>(
                "Body",
                _options.UsesPredefinedTemplate ? _options.BodyTemplate : message)
        });

        var url = string.Create(CultureInfo.InvariantCulture,
            $"https://api.twilio.com/2010-04-01/Accounts/{_options.AccountSid}/Messages.json");

        var response = await client.PostAsync(url, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Falha ao enviar SMS via Twilio: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Falha na Twilio API ao enviar SMS.");
        }
    }

    private static string Normalize(string to)
        => to.Trim().Length >= 1 && to[0] == '+' ? to : $"+{to.Trim()}";
}