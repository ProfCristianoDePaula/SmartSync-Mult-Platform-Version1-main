namespace Identity.Application.Auth;

/// <summary>
/// Efetiva o reset de senha com o token recebido por e-mail (link) ou o código
/// de 6 dígitos recebido por SMS.
/// </summary>
public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);