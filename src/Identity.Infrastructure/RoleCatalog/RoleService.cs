using Identity.Application.RoleCatalog;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.RoleCatalog;

/// <summary>
/// Catálogo de roles da plataforma. Lê as roles persistidas no banco (AspNetRoles)
/// e as ordena na ordem canônica de <see cref="Roles.All"/>, preservando qualquer
/// role extra criada por futuros endpoints de admin.
/// </summary>
public sealed class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RoleService(RoleManager<ApplicationRole> roleManager) => _roleManager = roleManager;

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken ct = default)
    {
        var stored = await _roleManager.Roles
            .Select(r => r.Name!)
            .ToListAsync(ct);

        return Roles.All
            .Where(stored.Contains)
            .Concat(stored
                .Where(name => !Roles.All.Contains(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
