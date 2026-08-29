namespace Identity.Application.Notifications;

/// <summary>
/// Configuração de SMS. O provedor é escolhido conforme o valor de "Provider":
/// - "twilio": envia via Twilio REST API (trial gratuito tem limitações);
/// - "log" (padrão/empty): implementação que só loga (dev, sem custo).
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string Provider { get; set; } = "log";
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;

    public bool TwilioConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) &&
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(FromNumber);

    /// <summary>
    /// Modo trial: contas trial só aceitam templates predefinidos da Twilio
    /// (ex.: "sms_2fa"). Quando preenchido, o corpo da mensagem NÃO é
    /// personalizável (o código gerado não aparece no SMS). Vazio = texto
    /// customizado (conta paga/upgraded).
    /// </summary>
    public bool UsesPredefinedTemplate =>
        !string.IsNullOrWhiteSpace(BodyTemplate);
}