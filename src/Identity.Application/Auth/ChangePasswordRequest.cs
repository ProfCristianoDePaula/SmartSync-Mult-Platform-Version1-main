namespace Identity.Application.Auth;

/// <summary>
/// Troca de senha do usuário autenticado (exige a senha atual).
/// </summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);