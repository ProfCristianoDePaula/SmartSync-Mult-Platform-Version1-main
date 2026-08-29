namespace Identity.Application.Auth;

/// <summary>
/// Solicitação de reenvio do e-mail de confirmação para uma conta NÃO
/// confirmada (ex.: flag "Não recebido o e-mail?" no login do Client).
/// A API responde sucesso mesmo se o e-mail não existir (evita enumeração).
/// </summary>
public sealed record ResendConfirmationEmailRequest(string Email);