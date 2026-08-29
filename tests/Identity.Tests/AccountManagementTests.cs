using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Identity.Application.Auth;
using Identity.Application.TenantModules;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Tests.Lab;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Fluxo end-to-end da gestão de conta (Etapa 11): reset de senha por e-mail e
/// por código SMS, troca de senha autenticada e logout (sessão única/geral).
/// Válidos para todas as roles.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AccountManagementTests
{
    private readonly IdentityApiFactory _factory;

    public AccountManagementTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RecuperarSenha_EnviaTokenPorEmail_EfetivaReset()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var client = _factory.CreateApiClient();
        _factory.EmailSender.Clear();

        // Passo 1: pede o reset por e-mail (resposta neutra).
        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(user.Email!));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        // Passo 2: extrai o token do link embutido no e-mail capturado.
        var email = Assert.Single(_factory.EmailSender.Messages);
        var token = EmailLinkToken(email.Body, "reset-password");
        Assert.False(string.IsNullOrWhiteSpace(token));

        // Passo 3: efetiva o reset com a nova senha.
        var newPassword = "Nova!Senha2026x";
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(user.Email!, token!, newPassword));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // Passo 4: login com a nova senha funciona; com a antiga, falha.
        var okLogin = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, newPassword));
        Assert.Equal(HttpStatusCode.OK, okLogin.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, TestData.UniquePassword()));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task RecuperarSenha_EmailInexistente_RespostaNeutra()
    {
        var client = _factory.CreateApiClient();
        _factory.EmailSender.Clear();
        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(TestData.UniqueEmail()));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Empty(_factory.EmailSender.Messages);
    }

    [Fact]
    public async Task RecuperarSenha_CodigoSmsCapturado_EfetivaReset()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        // Seta o telefone do usuário para o SMS ter destinatário válido.
        var userManager = sp.GetRequiredService<UserManager<Identity.Infrastructure.Persistence.Identity.ApplicationUser>>();
        var phone = "+5511999996666";
        await userManager.SetPhoneNumberAsync(user, phone);

        var client = _factory.CreateApiClient();
        _factory.SmsSender.Clear();

        // Passo 1: pede o código por SMS.
        var forgotSms = await client.PostAsJsonAsync("/api/auth/forgot-password/sms",
            new ForgotPasswordSmsRequest(user.Email!));
        Assert.Equal(HttpStatusCode.OK, forgotSms.StatusCode);

        // Passo 2: extrai o código de 6 dígitos do SMS capturado.
        var sms = Assert.Single(_factory.SmsSender.Messages);
        var match = Regex.Match(sms.Message, @"(\d{6})");
        Assert.True(match.Success, $"Código não encontrado em: {sms.Message}");

        // Passo 3: efetiva o reset com o código SMS.
        var newPassword = "Nova!Senha2026";
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(user.Email!, match.Value, newPassword));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, newPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task RecuperarSenha_CodigoSmsInvalido_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var client = _factory.CreateApiClient();
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(user.Email!, "000000", "NovaSenha!2026"));

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task TrocarSenha_Autenticado_ValidaSenhaAtual()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Manager);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Troca de senha com a senha atual correta.
        var newPassword = "OutraSenha!2026x";
        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(TestData.UniquePassword(), newPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        // A sessão (refresh token) foi revogada pelo change-password.
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // Login com a nova senha funciona.
        var fresh = _factory.CreateApiClient();
        var login = await fresh.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, newPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task TrocarSenha_SenhaAtualIncorreta_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Seller);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("senha-errada", "NovaSenha!2026"));

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
    }

    [Fact]
    public async Task Logout_RevogaReposFlagTokenDaSessao()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Delivery);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var logout = await client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevogaTodasAsSessoes()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.SuperAdmin);

        // Duas sessões independentes (dois logins → dois refresh tokens).
        var first = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());
        var second = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", first.AccessToken);

        var logoutAll = await client.PostAsJsonAsync("/api/auth/logout-all", new { });
        Assert.Equal(HttpStatusCode.OK, logoutAll.StatusCode);

        var refreshFirst = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshFirst.StatusCode);

        var refreshSecond = await client.PostAsJsonAsync("/api/auth/refresh-token",
            new RefreshRequest(second.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshSecond.StatusCode);
    }

    /// <summary>Extrai o parâmetro "token" de um link (ex.: reset-password/?token=...).</summary>
    private static string? EmailLinkToken(string body, string pathSegment)
    {
        const string marker = "token=";
        var index = body.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return null;

        var start = index + marker.Length;
        var end = body.IndexOf('"', start);
        if (end < 0)
            end = body.Length;

        var raw = body[start..end];
        return Uri.UnescapeDataString(raw);
    }
}

/// <summary>
/// Cadastro público de Client (autosserviço, Etapa 11): cria a conta vinculada
/// ao tenant com e-mail não confirmado e envia o link de confirmação.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RegisterTests
{
    private readonly IdentityApiFactory _factory;

    public RegisterTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Registrar_CriaClientComEmailNaoConfirmado()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);

        var client = _factory.CreateApiClient();
        _factory.EmailSender.Clear();

        var email = TestData.UniqueEmail("client");
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Cliente Teste", email, "Senha!2026x", tenant.Id.Value, "12345678901"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // E-mail de confirmação enviado com link contendo token.
        var sent = Assert.Single(_factory.EmailSender.Messages);
        Assert.Equal(email, sent.To);

        // Login bloqueado (conta ainda não confirmada).
        var login = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(email, "Senha!2026x"));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Registrar_EmailDuplicadoNoTenant_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var existing = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Outro Cliente", existing.Email!, "Senha!2026x", tenant.Id.Value, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registrar_TenantInexistente_Retorna400()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Cliente", TestData.UniqueEmail(), "Senha!2026x", Guid.NewGuid(), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registrar_ClientesNaoContamParaLimiteDeUsuariosDoPlano()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);

        var db = sp.GetRequiredService<IdentityDbContext>();
        var plan = Plan.Create(
            module.Id,
            $"Plano {Guid.NewGuid():N}",
            null,
            49.90m,
            499.00m,
            7,
            ["Dashboard"],
            maxBranches: 5,
            maxUsers: 1,
            maxStorageMb: 1024);
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var tokens = await TestData.LoginAsync(_factory, admin.Email!, TestData.UniquePassword());
        var adminClient = _factory.CreateApiClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var link = await adminClient.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/modules",
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);

        var client = _factory.CreateApiClient();

        // MaxUsers = 1, mas Clients (autosserviço) NÃO contam para o limite —
        // o cadastro público nunca é barrado por MaxUsers (decisão do usuário).
        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest($"Cliente {i}", TestData.UniqueEmail("cliente"), "Senha!2026x", tenant.Id.Value, TestData.UniqueCpf()));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }
}