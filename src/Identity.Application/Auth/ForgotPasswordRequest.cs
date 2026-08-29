namespace Identity.Application.Auth;

/// <summary>
/// Solicitação de reset de senha por e-mail (público). A resposta é neutra
/// (não revela se o e-mail existe) por segurança.
/// </summary>
public sealed record ForgotPasswordRequest(string Email);