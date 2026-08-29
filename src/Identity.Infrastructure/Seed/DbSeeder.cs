using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Seed;

/// <summary>
/// Seed de bootstrap da plataforma (Etapa 18), idempotente, chamado uma única
/// vez na inicialização da API dentro de um escopo de DI (logo após o
/// <see cref="IdentitySeeder"/>). Ordem de criação: Module → Plan → Tenant →
/// Branch → vínculo Tenant-Module → SuperAdmin.
/// <para>
/// Cada etapa checa a existência antes de criar (module por slug, plan por
/// nome no módulo, tenant por documento, filial por nome no tenant, vínculo por
/// (tenant, módulo) ativo e usuário por e-mail), então repetir a inicialização
/// nunca duplica dados.
/// </para>
/// </summary>
public static class DbSeeder
{
    // Chaves de idempotência e valores fixos do bootstrap.
    private const string ModuleName = "SmartSync Core";
    private const string ModuleSlug = "core";
    private const string PlanName = "Full Access";
    private const string PlatformName = "SmartSync Platform";
    private const string TenantCpf = "29558447803";
    private const string SuperAdminEmail = "sa@smartsync.com.br";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DbSeeder));

        // Garante as roles da plataforma (as mesmas da Etapa 03) antes de
        // atribuir qualquer uma a um usuário.
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new ApplicationRole(roleName));
        }

        // 1) Module "core".
        var module = await dbContext.Modules
            .IgnoreQueryFilters(["Active"])
            .FirstOrDefaultAsync(m => m.Slug == ModuleSlug, ct);
        if (module is null)
        {
            module = Module.Create(
                ModuleName,
                ModuleSlug,
                "Módulo núcleo da plataforma SmartSync (contratado por todos os tenants).");
            dbContext.Modules.Add(module);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("DbSeeder: módulo '{Module}' criado.", ModuleSlug);
        }

        // 2) Plan "Full Access" (R$ 0,00; limites null = sem limite).
        var plan = await dbContext.Plans
            .IgnoreQueryFilters(["Active"])
            .FirstOrDefaultAsync(p => p.ModuleId == module.Id && p.Name == PlanName, ct);
        if (plan is null)
        {
            plan = Plan.Create(
                module.Id,
                PlanName,
                "Acesso total à plataforma SmartSync (uso interno).",
                0m,
                0m,
                0,
                ["Acesso total", "Sem limites de filiais, usuários e armazenamento"],
                null,
                null,
                null);
            dbContext.Plans.Add(plan);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("DbSeeder: plano '{Plan}' criado no módulo '{Module}'.", PlanName, ModuleSlug);
        }

        // 3) Tenant "SmartSync Platform".
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters(["Active"])
            .FirstOrDefaultAsync(t => t.Documento.Numero == TenantCpf, ct);
        if (tenant is null)
        {
            tenant = Tenant.Create(
                PlatformName,
                PlatformName,
                Documento.Create(TipoPessoa.Fisica, TenantCpf),
                Email.Create(SuperAdminEmail));
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("DbSeeder: tenant '{Tenant}' criado.", PlatformName);
        }

        // 4) Branch "SmartSync Platform" (sede do tenant de bootstrap).
        var branch = await dbContext.Branches
            .IgnoreQueryFilters(["Active"])
            .FirstOrDefaultAsync(b => b.TenantId == tenant.Id && b.Name == PlatformName, ct);
        if (branch is null)
        {
            branch = Branch.Create(
                tenant.Id,
                PlatformName,
                new Address(
                    "Rua Albertina Balthazar de Oliveira",
                    "911",
                    null,
                    "Bela Vista",
                    "Jaú",
                    "SP",
                    "17206441"),
                new Contact("1436220000", Email.Create(SuperAdminEmail)));
            dbContext.Branches.Add(branch);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("DbSeeder: filial '{Branch}' criada.", PlatformName);
        }

        // 5) Vínculo Tenant-Module (no máximo UM ativo por (tenant, module)).
        var hasActiveLink = await dbContext.TenantModules.AnyAsync(
            tm => tm.TenantId == tenant.Id && tm.ModuleId == module.Id && tm.Status == TenantModuleStatus.Active,
            ct);
        if (!hasActiveLink)
        {
            var link = TenantModule.Create(tenant.Id, module.Id, plan.Id, DateTime.UtcNow);
            dbContext.TenantModules.Add(link);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("DbSeeder: vínculo tenant '{Tenant}' ↔ módulo '{Module}' criado.", PlatformName, ModuleSlug);
        }

        // 6) SuperAdmin da plataforma (usuário GLOBAL, sem tenant).
        var admin = await userManager.FindByEmailAsync(SuperAdminEmail);
        if (admin is null)
        {
            var password = ResolveSuperAdminPassword(configuration, environment, logger);
            if (!string.IsNullOrWhiteSpace(password))
            {
                admin = new ApplicationUser
                {
                    UserName = SuperAdminEmail,
                    Email = SuperAdminEmail,
                    FullName = PlatformName,
                    TenantId = null,
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };
                var result = await userManager.CreateAsync(admin, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
                    logger.LogInformation("DbSeeder: SuperAdmin {Email} criado.", SuperAdminEmail);
                }
                else
                {
                    logger.LogWarning("DbSeeder: falha ao criar SuperAdmin: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    /// <summary>
    /// Resolve a senha do SuperAdmin de bootstrap: prioriza a variável de
    /// ambiente <c>SEED_SUPERADMIN_PASSWORD</c> (e, por compatibilidade, a
    /// seção <c>SeedSuperAdmin:Password</c>). Se nada for informado, usa o
    /// default <c>Sm@rtSync2026!</c> APENAS em Development; fora de Development
    /// o usuário não é criado (a configuração é obrigatória em produção).
    /// </summary>
    private static string? ResolveSuperAdminPassword(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var password = configuration["SEED_SUPERADMIN_PASSWORD"]
            ?? configuration["SeedSuperAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(password))
            return password;

        if (environment.IsDevelopment())
            return "Sm@rtSync2026!";

        logger.LogWarning(
            "DbSeeder: SEED_SUPERADMIN_PASSWORD não definida fora de Development. " +
            "O usuário SuperAdmin não será criado.");
        return null;
    }
}
