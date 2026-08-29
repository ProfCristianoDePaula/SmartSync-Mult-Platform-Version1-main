namespace Identity.Application.Auth;

/// <summary>
/// Solicita o código de reset de senha por SMS (público). A resposta é neutra.
/// O código é gerado com <c>GeneratePasswordResetTokenAsync</c> e enviado via
/// <c>ISmsSender</c>; formato: 6 dígitos numéricos, expiração padrão do
/// provider de token (10 minutos).
/// </summary>
public sealed record ForgotPasswordSmsRequest(string Email);