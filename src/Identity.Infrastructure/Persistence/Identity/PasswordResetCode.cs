using Identity.Domain.Common;

namespace Identity.Infrastructure.Persistence.Identity;

/// <summary>
/// Código de reset de senha por SMS (6 dígitos). Apenas o hash SHA-256 é
/// persistido; o valor numérico só viaja no SMS. Expira após o tempo definido
/// em <see cref="PasswordResetCodeConstants.Lifetime"/>.
/// </summary>
public sealed class PasswordResetCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Hash SHA-256 do código de 6 dígitos (não armazenamos o valor).</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

/// <summary>Constantes de expiração dos tokens de senha.</summary>
public static class PasswordResetCodeConstants
{
    /// <summary>Tempo de vida do código SMS de reset de senha.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
}