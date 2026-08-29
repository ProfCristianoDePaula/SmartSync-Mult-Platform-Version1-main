using Identity.Domain.Common;

namespace Identity.Infrastructure.Persistence.Identity;

/// <summary>
/// Refresh token (a versão hashada é persistida; o valor em claro só viaja
/// na resposta). Suporta rotação: um token é revogado quando substituído.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Hash SHA-256 do token (não armazenamos o valor plano).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Revogação explícita (rotação, logout, revogação admin).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Hash do token que o substituiu (para depuração de rotação).</summary>
    public string? ReplacedByTokenHash { get; set; }
}