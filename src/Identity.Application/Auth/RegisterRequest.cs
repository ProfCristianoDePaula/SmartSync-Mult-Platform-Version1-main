namespace Identity.Application.Auth;

/// <summary>
/// Cadastro público de um Client (autosserviço). O Client pertence a um tenant
/// (informado via tenantId), nasce com e-mail NÃO confirmado e só pode logar
/// após confirmar o e-mail recebido por IEmailSender.
/// </summary>
public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    Guid TenantId,
    string? Document);