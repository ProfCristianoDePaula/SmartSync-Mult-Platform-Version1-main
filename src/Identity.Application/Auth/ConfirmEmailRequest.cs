namespace Identity.Application.Auth;

/// <summary>
/// Confirmação de e-mail: quem recebe o token nativo do Identity
/// (GenerateEmailConfirmationTokenAsync emitido ao criar a conta) informa
/// e-mail + token retornado no link clicado.
/// </summary>
public sealed record ConfirmEmailRequest(string Email, string Token);