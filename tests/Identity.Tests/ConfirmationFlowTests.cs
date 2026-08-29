using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Identity.Application.Auth;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence;
using Identity.Tests.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Fluxo end-to-end de confirmação de conta (Etapa 06/09): reenvio de e-mail de
/// confirmação com token e validação de celular via código SMS. Os senders reais
/// são substituídos por capturadores em memória na suite de integração.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ConfirmationFlowTests
{
    private readonly IdentityApiFactory _factory;

    public ConfirmationFlowTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ConfirmarEmail_ComTokenCapturado_AtivaConta()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(
            sp, tenant.Id, Roles.Client, emailConfirmed: false);

        var client = _factory.CreateApiClient();

        // Passo 1: reenvia o e-mail de confirmação (endpoint público, resposta neutra).
        var resend = await client.PostAsJsonAsync("/api/auth/resend-confirmation-email",
            new ResendConfirmationEmailRequest(user.Email!));
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        // Passo 2: extrai o token do e-mail capturado pelo sender de teste.
        var email = Assert.Single(_factory.EmailSender.Messages);
        var token = email.ExtractConfirmationToken();
        Assert.False(string.IsNullOrWhiteSpace(token));

        // Passo 3: confirma o e-mail com o token.
        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest(user.Email!, token!));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // Passo 4: agora o login funciona (conta confirmada).
        var login = await client.PostAsJsonAsync("/api/auth/login",
            TestData.Login(user.Email!, TestData.UniquePassword()));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ConfirmarEmail_TokenInvalido_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(
            sp, tenant.Id, Roles.Client, emailConfirmed: false);

        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest(user.Email!, "token-invalido"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidarCelular_CodigoSmsCapturado_PersisteNumero()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var phone = "+5511999997777";

        // Passo 1: pede o código via SMS (exige e-mail confirmado — policy).
        var send = await client.PostAsJsonAsync("/api/auth/send-sms-code",
            new SendSmsCodeRequest(phone));
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        // Passo 2: extrai o código (6 dígitos) do SMS capturado.
        var sms = Assert.Single(_factory.SmsSender.Messages);
        var match = Regex.Match(sms.Message, @"(\d{6})");
        Assert.True(match.Success, $"Código não encontrado em: {sms.Message}");

        // Passo 3: confirma o celular com o código.
        var confirm = await client.PostAsJsonAsync("/api/auth/confirm-phone",
            new ConfirmPhoneRequest(phone, match.Value));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // Passo 4: número persistido e marcado como confirmado no banco.
        // Usa um scope + AsNoTracking: o contexto original já possui o usuário
        // no change tracker (criado no teste), então FindAsync retornaria o
        // estado antigo sem consultar o banco.
        using var readScope = _factory.Services.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var persisted = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        Assert.Equal(phone, persisted.PhoneNumber);
        Assert.True(persisted.PhoneNumberConfirmed);
    }

    [Fact]
    public async Task ValidarCelular_CodigoIncorreto_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/confirm-phone",
            new ConfirmPhoneRequest("+5511999997777", "000000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public static class CapturedEmailExtensions
{
    /// <summary>
    /// Extrai o parâmetro "token" do link de confirmação embutido no corpo do
    /// e-mail (formato: .../confirm-email?email=...&amp;token=...).
    /// </summary>
    public static string? ExtractConfirmationToken(this CapturedEmail email)
    {
        const string marker = "token=";
        var body = email.Body;

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