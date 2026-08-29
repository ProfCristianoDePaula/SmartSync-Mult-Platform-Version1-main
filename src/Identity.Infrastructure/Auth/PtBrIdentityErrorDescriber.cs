using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// IdentityErrorDescriber com mensagens em português (pt-BR) para os erros
/// comuns do ASP.NET Core Identity. Substitui os textos padrão em inglês.
/// </summary>
public sealed class PtBrIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error("default", "Ocorreu um erro desconhecido.");

    public override IdentityError ConcurrencyFailure()
        => Error("concurrency", "A entidade já foi alterada enquanto você a editava.");

    public override IdentityError PasswordMismatch() => Error("password_mismatch", "Senha incorreta.");

    public override IdentityError InvalidToken() => Error("invalid_token", "Token inválido.");

    public override IdentityError RecoveryCodeRedemptionFailed()
        => Error("recovery_code_redemption_failed", "Falha ao usar o código de recuperação.");

    public override IdentityError LoginAlreadyAssociated()
        => Error("login_already_associated", "Este login externo já está associado a outra conta.");

    public override IdentityError InvalidUserName(string? userName)
        => Error("invalid_user_name", $"O nome de usuário '{userName}' é inválido, só pode conter letras e dígitos.");

    public override IdentityError InvalidEmail(string? email)
        => Error("invalid_email", $"O e-mail '{email}' é inválido.");

    public override IdentityError DuplicateUserName(string? userName)
        => Error("duplicate_user_name", $"O nome de usuário '{userName}' já está em uso.");

    public override IdentityError DuplicateEmail(string? email)
        => Error("duplicate_email", $"O e-mail '{email}' já está em uso.");

    public override IdentityError InvalidRoleName(string? role)
        => Error("invalid_role_name", $"A role '{role}' é inválida.");

    public override IdentityError DuplicateRoleName(string? role)
        => Error("duplicate_role_name", $"A role '{role}' já existe.");

    public override IdentityError UserAlreadyHasPassword()
        => Error("user_already_has_password", "Este usuário já possui uma senha.");

    public override IdentityError UserLockoutNotEnabled()
        => Error("user_lockout_not_enabled", "Este usuário não tem bloqueio habilitado.");

    public override IdentityError UserAlreadyInRole(string? role)
        => Error("user_already_in_role", $"O usuário já está na role '{role}'.");

    public override IdentityError UserNotInRole(string? role)
        => Error("user_not_in_role", $"O usuário não está na role '{role}'.");

    public override IdentityError PasswordTooShort(int length)
        => Error("password_too_short", $"A senha deve conter ao menos {length} caracteres.");

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Error("password_requires_non_alphanumeric", "A senha deve conter ao menos um caractere especial.");

    public override IdentityError PasswordRequiresDigit()
        => Error("password_requires_digit", "A senha deve conter ao menos um dígito numérico ('0'-'9').");

    public override IdentityError PasswordRequiresLower()
        => Error("password_requires_lower", "A senha deve conter ao menos uma letra minúscula ('a'-'z').");

    public override IdentityError PasswordRequiresUpper()
        => Error("password_requires_upper", "A senha deve conter ao menos uma letra maiúscula ('A'-'Z').");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
        => Error("password_requires_unique_chars", $"A senha deve conter ao menos {uniqueChars} caracteres únicos.");

    private static IdentityError Error(string code, string description)
        => new() { Code = code, Description = description };
}