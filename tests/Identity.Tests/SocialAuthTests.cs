using Identity.Application.Auth;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence.Identity;
using Identity.Tests.Lab;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Login social (Google/Facebook) — Etapa 21. Como as credenciais OAuth reais
/// não existem neste ambiente (os schemes não são registrados em testes), o
/// fluxo é exercitado diretamente no <see cref="ISocialAuthService"/> (Postgres
/// real via Testcontainers). Cobre os três cenários da correção:
/// 1) primeiro login cria o Client e grava o vínculo (provider, providerKey);
/// 2) login recorrente reutiliza a conta sem recriar nada;
/// 3) e-mail que existe mas nunca foi vinculado ao provedor → conflito
/// (anti-takeover) — agora com resultado tipado (e não 401 genérico).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class SocialAuthTests
{
    private readonly IdentityApiFactory _factory;

    public SocialAuthTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PrimeiroLoginSocial_CriaClientEGravaVinculoDoProvedor()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();

        var email = TestData.UniqueEmail("social");
        var providerKey = $"google-sub-{Guid.NewGuid():N}";

        var result = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", providerKey, email, "Cliente Social", tenant.Id.Value));

        // Sucesso: par de tokens emitido.
        Assert.NotNull(result.Token);
        Assert.Null(result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.Token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Token.RefreshToken));

        // Client criado e VÍNCULO gravado em AspNetUserLogins — o passo que
        // garante que o login recorrente (FindByLoginAsync) reencontra a conta.
        var linked = await userManager.FindByLoginAsync("Google", providerKey);
        Assert.NotNull(linked);
        Assert.Equal(tenant.Id, linked!.TenantId);
        Assert.True(await userManager.IsInRoleAsync(linked, Roles.Client));
        Assert.True(linked.EmailConfirmed);

        // Apenas uma conta com esse e-mail.
        var normalized = userManager.NormalizeEmail(email);
        var count = await db.Users.CountAsync(u => u.NormalizedEmail == normalized);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SegundoLoginSocial_MesmoProvedor_ReutilizaContaSemCriarNada()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<Identity.Infrastructure.Persistence.IdentityDbContext>();

        var email = TestData.UniqueEmail("social");
        var providerKey = $"google-sub-{Guid.NewGuid():N}";

        // Primeiro login (cria o Client).
        var first = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", providerKey, email, "Cliente Social", tenant.Id.Value));
        Assert.NotNull(first.Token);

        // Login recorrente: mesmo usuário voltando pelo mesmo provedor.
        var second = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", providerKey, email, "Cliente Social", tenant.Id.Value));
        Assert.NotNull(second.Token);
        Assert.Null(second.Error);

        // Nada foi recriado: continua existindo UMA única conta com o e-mail.
        var normalized = userManager.NormalizeEmail(email);
        var count = await db.Users.CountAsync(u => u.NormalizedEmail == normalized);
        Assert.Equal(1, count);

        // A mesma conta foi reutilizada (não criou outra).
        var linked = await userManager.FindByLoginAsync("Google", providerKey);
        Assert.NotNull(linked);
        Assert.Equal(tenant.Id, linked!.TenantId);
    }

    [Fact]
    public async Task LoginSocial_EmailExistenteSemVinculo_RetornaEmailConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();

        // Conta local (senha) com este e-mail — NUNCA vinculada a um provedor.
        var local = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var email = local.Email!;

        var result = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", $"google-sub-{Guid.NewGuid():N}", email, null, tenant.Id.Value));

        // Conflito real anti-takeover (Etapa 05), agora tipado (→ 401 no HTTP).
        Assert.Null(result.Token);
        Assert.Equal(SocialAuthError.EmailConflict, result.Error);
    }

    [Fact]
    public async Task LoginSocial_UsuarioVinculadoSemRoleClient_RetornaNotClient()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        // Usuário interno (Manager) com vínculo social, mas sem a role Client.
        var internalUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Manager);
        var providerKey = $"google-sub-{Guid.NewGuid():N}";
        var linked = await userManager.AddLoginAsync(
            internalUser, new UserLoginInfo("Google", providerKey, "Google"));
        Assert.True(linked.Succeeded);

        var result = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", providerKey, internalUser.Email!, null, tenant.Id.Value));

        Assert.Null(result.Token);
        Assert.Equal(SocialAuthError.NotClient, result.Error);
    }

    [Fact]
    public async Task LoginSocial_TenantInexistente_RetornaTenantNotFound()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();

        var result = await socialAuth.LoginAsync(
            new SocialLoginRequest(
                "Google",
                $"google-sub-{Guid.NewGuid():N}",
                TestData.UniqueEmail("social"),
                null,
                Guid.NewGuid()));

        Assert.Null(result.Token);
        Assert.Equal(SocialAuthError.TenantNotFound, result.Error);
    }

    [Fact]
    public async Task LoginSocial_ContaVinculadaAOutroTenant_RetornaTenantMismatch()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();

        // Client criado no tenant A via primeiro login social.
        var email = TestData.UniqueEmail("social");
        var providerKey = $"google-sub-{Guid.NewGuid():N}";
        var first = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", providerKey, email, "Cliente A", tenantA.Id.Value));
        Assert.NotNull(first.Token);

        // Mesmo usuário tenta entrar com o state de OUTRO tenant (B).
        var second = await socialAuth.LoginAsync(
            new SocialLoginRequest("Google", providerKey, email, "Cliente A", tenantB.Id.Value));

        Assert.Null(second.Token);
        Assert.Equal(SocialAuthError.TenantMismatch, second.Error);
    }

    [Fact]
    public async Task LoginSocial_ProvedorNaoSuportado_RetornaProviderUnsupported()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();

        var result = await socialAuth.LoginAsync(
            new SocialLoginRequest("Apple", "xyz", TestData.UniqueEmail("social"), null, tenant.Id.Value));

        Assert.Null(result.Token);
        Assert.Equal(SocialAuthError.ProviderUnsupported, result.Error);
    }
}
