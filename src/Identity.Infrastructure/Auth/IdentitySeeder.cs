using Identity.Domain.Common;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Semeia as roles da plataforma (Etapa 03). Aplica as migrations pendentes
/// antes de qualquer seed. A criação do SuperAdmin de bootstrap e dos dados
/// da plataforma (module/plan/tenant/filial/vínculo) fica a cargo do
/// <see cref="Seed.DbSeeder"/>.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdentitySeeder));

        // Bootstrap: aplica as migrations pendentes antes de qualquer seed.
        await dbContext.Database.MigrateAsync(ct);

        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole(roleName));
        }
    }
}