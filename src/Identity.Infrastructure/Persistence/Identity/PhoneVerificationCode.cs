namespace Identity.Infrastructure.Persistence.Identity;

/// <summary>
/// Código de verificação de celular (6 dígitos) enviado por SMS no fluxo
/// send-sms-code/confirm-phone. Apenas o hash SHA-256 é persistido; o valor
/// numérico só viaja no SMS. Expira após o tempo definido em
/// <see cref="PhoneVerificationCodeConstants.Lifetime"/>.
/// </summary>
public sealed class PhoneVerificationCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Celular para o qual o código foi enviado (vínculo na confirmação).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Hash SHA-256 do código de 6 dígitos (não armazenamos o valor).</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}

/// <summary>Constantes de expiração dos códigos de verificação de celular.</summary>
public static class PhoneVerificationCodeConstants
{
    /// <summary>Tempo de vida do código SMS de verificação de celular.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
}
