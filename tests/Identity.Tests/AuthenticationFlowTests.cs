using System.Net;
using System.Net.Http.Json;
using System.Text;
using Identity.Application.Auth;
using Identity.Domain.Common;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Fluxo de autenticação: login (sucesso/falha/email não confirmado), rotação
/// de refresh token e restrição de acesso por role/policy (401/403).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthenticationFlowTests
{
    private readonly IdentityApiFactory _factory;

    public AuthenticationFlowTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_ComCredenciaisValidas_RetornaTokens()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await TestData.CreateTenantAsync(scope.ServiceProvider);
        var user = await TestData.CreateUserAsync(scope.ServiceProvider, tenant.Id, Roles.Client);

        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, TestData.UniquePassword()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.True(token.ExpiresInSeconds > 0);
    }

    [Fact]
    public async Task Login_SenhaIncorreta_Retorna401()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await TestData.CreateTenantAsync(scope.ServiceProvider);
        var user = await TestData.CreateUserAsync(scope.ServiceProvider, tenant.Id, Roles.Client);

        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, "SenhaErrada!1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmailNaoConfirmado_Retorna401()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await TestData.CreateTenantAsync(scope.ServiceProvider);
        var user = await TestData.CreateUserAsync(
            scope.ServiceProvider, tenant.Id, Roles.Client, emailConfirmed: false);

        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, TestData.UniquePassword()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotacionaToken_AntigoDeixaDeValer()
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await TestData.CreateTenantAsync(scope.ServiceProvider);
        var user = await TestData.CreateUserAsync(scope.ServiceProvider, tenant.Id, Roles.Client);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();

        // Primeiro refresh: gera novo par.
        var refresh1 = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh1.StatusCode);
        var rotated = await refresh1.Content.ReadFromJsonAsync<TokenResponse>();

        // Reutilizar o refresh antigo deve falhar (rotação revoga o anterior).
        var refresh2 = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh2.StatusCode);

        // O novo refresh continua válido.
        var refresh3 = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(rotated!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh3.StatusCode);
    }

    // ==================== Restrição de acesso por role/policy ====================

    [Fact]
    public async Task SendSmsCode_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/send-sms-code",
            new SendSmsCodeRequest("+5511999998888"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendSmsCode_EmailNaoConfirmado_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(
            sp, tenant.Id, Roles.Client, emailConfirmed: false);

        // Como login bloqueia conta não confirmada, emitimos o token diretamente
        // (mesma emissão usada na produção) para exercitar a policy.
        var tokenService = sp.GetRequiredService<Identity.Infrastructure.Security.TokenService>();
        var access = await tokenService.CreateAccessTokenAsync(user, [Roles.Client]);

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);

        var response = await client.PostAsJsonAsync("/api/auth/send-sms-code",
            new SendSmsCodeRequest("+5511999998888"));

        // Autenticado, mas a policy "email-confirmed" barra (claim false).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmPhone_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/confirm-phone",
            new ConfirmPhoneRequest("+5511999998888", "123456"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointsPublicos_SemToken_NaoSaoBloqueados()
    {
        var client = _factory.CreateApiClient();

        // JWKS é público (consumidores validam tokens).
        var jwks = await client.GetAsync("/api/auth/jwks");
        Assert.Equal(HttpStatusCode.OK, jwks.StatusCode);

        // Health check é público.
        var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}