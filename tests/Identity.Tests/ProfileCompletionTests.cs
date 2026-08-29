using System.Net;
using System.Net.Http.Json;
using System.Text;
using Identity.Application.Auth;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence.Identity;
using Identity.Tests.Lab;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Onboarding pós-login social (Etapa 22): a API informa quando o Client ainda
/// não completou o cadastro (campo requiresProfileCompletion do TokenResponse +
/// claim profile_complete do JWT) e oferece o POST /api/auth/profile para
/// completar (documento obrigatório), reemitindo tokens com a claim atualizada.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ProfileCompletionTests
{
    private readonly IdentityApiFactory _factory;

    public ProfileCompletionTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LoginSocial_ClientSemDocumento_RetornaRequiresProfileCompletion()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var socialAuth = sp.GetRequiredService<ISocialAuthService>();

        var result = await socialAuth.LoginAsync(
            new SocialLoginRequest(
                "Google",
                $"google-sub-{Guid.NewGuid():N}",
                TestData.UniqueEmail("social"),
                "Cliente Social",
                tenant.Id.Value));

        // 1º login social cria o Client sem documento → cadastro incompleto.
        Assert.NotNull(result.Token);
        Assert.True(result.Token.RequiresProfileCompletion);
        Assert.Contains("\"profile_complete\":\"false\"", DecodeJwtPayload(result.Token.AccessToken));
    }

    [Fact]
    public async Task LoginLocal_ComDocumento_RetornaRequiresProfileCompletionFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client, document: TestData.UniqueCpf());

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        Assert.False(tokens.RequiresProfileCompletion);
        Assert.Contains("\"profile_complete\":\"true\"", DecodeJwtPayload(tokens.AccessToken));
    }

    [Fact]
    public async Task CompletarPerfil_ComDocumento_ReemiteTokensEAtualizaClaim()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());
        Assert.True(tokens.RequiresProfileCompletion);

        var cpf = TestData.UniqueCpf();
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/profile",
            new CompleteProfileRequest("Nome Completo", cpf));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(updated);
        Assert.False(updated.RequiresProfileCompletion);
        Assert.Contains("\"profile_complete\":\"true\"", DecodeJwtPayload(updated.AccessToken));

        // Sessão antiga revogada: o refresh token anterior não vale mais.
        var refreshOld = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshOld.StatusCode);

        // O refresh token novo continua válido.
        client.DefaultRequestHeaders.Authorization = null;
        var refreshNew = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(updated.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshNew.StatusCode);

        // Persistido no banco (documento + nome atualizados). AsNoTracking evita
        // que o change tracker do escopo do teste devolva a instância obsoleta
        // criada no início (que ainda tinha Document nulo).
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var dbUser = await userManager.Users.AsNoTracking().FirstAsync(u => u.Email == user.Email);
        Assert.Equal(cpf, dbUser.Document);
        Assert.Equal("Nome Completo", dbUser.FullName);
    }

    [Fact]
    public async Task CompletarPerfil_SemDocumento_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/profile",
            new CompleteProfileRequest("Nome Completo", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompletarPerfil_DocumentoDuplicadoNoTenant_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);

        // Outro usuário do MESMO tenant já usa este CPF.
        var cpf = TestData.UniqueCpf();
        await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client, document: cpf);

        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/profile",
            new CompleteProfileRequest(null, cpf));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompletarPerfil_DocumentoIgualEmOutroTenant_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        // Mesmo CPF em OUTRO tenant é permitido (unicidade por tenant — Etapa 04).
        var cpf = TestData.UniqueCpf();
        await TestData.CreateUserAsync(sp, tenantA.Id, Roles.Client, document: cpf);

        var userB = await TestData.CreateUserAsync(sp, tenantB.Id, Roles.Client);
        var tokens = await TestData.LoginAsync(_factory, userB.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/profile",
            new CompleteProfileRequest(null, cpf));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompletarPerfil_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/profile",
            new CompleteProfileRequest(null, TestData.UniqueCpf()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompletarPerfil_UsuarioNaoClient_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Manager);
        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/profile",
            new CompleteProfileRequest(null, TestData.UniqueCpf()));

        // Endpoint exclusivo da role Client.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
