using System.Net.Http.Json;
using System.Text;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Identity.Infrastructure.Seed;
using Identity.Tests.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Seed de bootstrap da plataforma (Etapa 18): valida que o DbSeeder cria o
/// Module "core", o Plan "Full Access" (limites null = sem limite), o Tenant
/// "SmartSync Platform", a filial, o vínculo ativo tenant↔módulo e o SuperAdmin
/// sa@smartsync.com.br — tudo idempotente — e que o login desse SuperAdmin
/// devolve um JWT sem a claim tenant_id (usuário global).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class BootstrapSeederTests
{
    private const string SuperAdminEmail = "sa@smartsync.com.br";

    private readonly IdentityApiFactory _factory;

    public BootstrapSeederTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Seed_CriaDadosDaPlataforma()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var module = await db.Modules.IgnoreQueryFilters(["Active"])
            .SingleAsync(m => m.Slug == "core");
        Assert.Equal("SmartSync Core", module.Name);

        var plan = await db.Plans.IgnoreQueryFilters(["Active"])
            .SingleAsync(p => p.ModuleId == module.Id && p.Name == "Full Access");
        Assert.Equal(0m, plan.MonthlyPrice);
        Assert.Equal(0m, plan.AnnualPrice);
        Assert.Equal(0, plan.TrialDays);
        Assert.Null(plan.MaxBranches);
        Assert.Null(plan.MaxUsers);
        Assert.Null(plan.MaxStorageMb);

        var tenant = await db.Tenants.IgnoreQueryFilters(["Active"])
            .SingleAsync(t => t.Documento.Numero == "29558447803");
        Assert.Equal(TipoPessoa.Fisica, tenant.Documento.Tipo);
        Assert.Equal("SmartSync Platform", tenant.TradeName);

        await db.Branches.IgnoreQueryFilters(["Active"])
            .SingleAsync(b => b.TenantId == tenant.Id && b.Name == "SmartSync Platform");
        Assert.Equal("17206441", (await db.Branches.IgnoreQueryFilters(["Active"])
            .SingleAsync(b => b.TenantId == tenant.Id)).Address.PostalCode);

        await db.TenantModules.SingleAsync(tm =>
            tm.TenantId == tenant.Id && tm.ModuleId == module.Id && tm.Status == TenantModuleStatus.Active);

        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var admin = await userManager.FindByEmailAsync(SuperAdminEmail);
        Assert.NotNull(admin);
        Assert.Null(admin!.TenantId);
        Assert.True(admin.EmailConfirmed);
        Assert.True(await userManager.IsInRoleAsync(admin, Roles.SuperAdmin));
    }

    [Fact]
    public async Task Seed_Repetido_DeveSerIdempotente()
    {
        var services = _factory.Services;

        // Roda o seed uma segunda vez — nenhum dado deve ser duplicado.
        await DbSeeder.SeedAsync(services);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        Assert.Equal(1, await db.Modules.IgnoreQueryFilters(["Active"]).CountAsync(m => m.Slug == "core"));
        Assert.Equal(1, await db.Plans.IgnoreQueryFilters(["Active"]).CountAsync(p => p.Name == "Full Access"));
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters(["Active"]).CountAsync(t => t.Documento.Numero == "29558447803"));
        Assert.Equal(1, await db.Branches.IgnoreQueryFilters(["Active"]).CountAsync(b => b.Name == "SmartSync Platform"));

        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var matches = await userManager.Users.CountAsync(u => u.Email == SuperAdminEmail);
        Assert.Equal(1, matches);
    }

    [Fact]
    public async Task Login_SuperAdminBootstrap_TokenSemTenantId()
    {
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();
        var password = configuration["SEED_SUPERADMIN_PASSWORD"]
            ?? configuration["SeedSuperAdmin:Password"]
            ?? "Sm@rtSync2026!";

        var tokens = await TestData.LoginAsync(_factory, SuperAdminEmail, password);

        var payload = DecodeJwtPayload(tokens.AccessToken);
        Assert.DoesNotContain("tenant_id", payload);
        Assert.Contains(Roles.SuperAdmin, payload);
        Assert.Contains(SuperAdminEmail, payload);
    }

    private static string DecodeJwtPayload(string accessToken)
    {
        var payload = accessToken.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        var padded = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
