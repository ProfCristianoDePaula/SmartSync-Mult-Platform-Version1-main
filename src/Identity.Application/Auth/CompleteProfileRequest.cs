namespace Identity.Application.Auth;

/// <summary>
/// Completa o cadastro do Client autenticado (onboarding pós-login social).
/// O documento (CPF/CNPJ) é obrigatório; o nome é opcional (atualiza quando
/// informado). Em sucesso o serviço reemite o par de tokens com a claim
/// "profile_complete" atualizada.
/// </summary>
public sealed record CompleteProfileRequest(
    string? FullName,
    string? Document);
