using Identity.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Persistence.Identity;

/// <summary>
/// Usuário customizado da plataforma.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Tenant ao qual o usuário pertence. Nulo para SuperAdmin (global).</summary>
    public TenantId? TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Documento (CPF ou CNPJ) conforme a role do usuário.</summary>
    public string? Document { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Cadastro completo do Client: documento (CPF/CNPJ) + nome preenchidos.
    /// Usado na claim "profile_complete" do JWT e no campo
    /// "requiresProfileCompletion" do TokenResponse (onboarding no frontend).
    /// </summary>
    public bool ProfileComplete =>
        !string.IsNullOrWhiteSpace(Document) &&
        !string.IsNullOrWhiteSpace(FullName);
}