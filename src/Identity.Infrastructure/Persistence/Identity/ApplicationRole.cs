using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Persistence.Identity;

/// <summary>Role da plataforma (Guid como chave).</summary>
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() : base() { }

    public ApplicationRole(string roleName) : base(roleName)
    {
        NormalizedName = roleName.ToUpperInvariant();
    }
}