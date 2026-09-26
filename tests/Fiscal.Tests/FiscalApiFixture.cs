using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Fiscal.Application.IntegrationServices;
using Fiscal.Domain.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Fixture compartilhada (collection "integration") — Postgres 100% real via
/// Testcontainers + host de teste com JWT assinado por chave RSA da própria
/// fixture e gate de módulo substituído por fake configurável (o comportamento
/// HTTP do Identity é coberto pela suíte do Identity).
/// </summary>
public sealed class FiscalApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("fiscal")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public RSA SigningRsa { get; } = RSA.Create(2048);
    public const string Issuer = "https://identity.test";
    public const string Audience = "fiscal-tests";
    public static readonly Guid TenantA = Guid.NewGuid();
    public static readonly Guid TenantB = Guid.NewGuid();
    public static Guid AdminUserId { get; } = Guid.NewGuid();

    /// <summary>Controla o fake do gate de módulo (fail-closed como o real).</summary>
    public bool ModuleActive { get; set; }

    /// <summary>Controla o fake de posse de filial (fail-closed como o real).</summary>
    public bool FilialAllowed { get; set; } = true;

    /// <summary>Respostas SOAP do fake SEFAZ (Fiscal-10). Padrão: falha fechada.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage> SefazResponder { get; set; } =
        _ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);

    /// <summary>Quando true, o validador XSD oficial é pulado (Fiscal-10, sem pacote).</summary>
    public bool PularXsdOficial { get; set; }

    /// <summary>Respostas do Sefin fake (Fiscal-12). Padrão: falha fechada.</summary>
    public Func<string, string, (bool HttpOk, string Corpo)> NfseResponder { get; set; } =
        (_, _) => (false, "{}");

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var fixture = this;
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                // UseSetting tem precedência sobre appsettings/user-secrets/env.
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
                builder.UseSetting("Jwt:Issuer", Issuer);
                builder.UseSetting("Jwt:Audience", Audience);
                builder.UseSetting("Identity:BaseUrl", "http://localhost:1"); // nunca chamado (fake)

                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IModuleAccessChecker>(
                        new FakeModuleChecker(() => fixture.ModuleActive));
                    services.AddSingleton<Fiscal.Application.IntegrationServices.IFilialAccessChecker>(
                        new FakeFilialChecker(() => fixture.FilialAllowed));
                    services.AddSingleton<Fiscal.Infrastructure.Sefaz.ISefazConnectionFactory>(
                        new FakeSefazConnectionFactory(() => fixture.SefazResponder));
                    services.AddSingleton<Fiscal.Infrastructure.Nfse.INfseHttpTransport>(
                        new FakeNfseTransport((baseUrl, path, json) => fixture.NfseResponder(baseUrl, path)));
                    services.AddSingleton<Fiscal.Application.Xml.IXsdValidator>(sp =>
                        new FakeXsdValidator(() => fixture.PularXsdOficial,
                            new Fiscal.Infrastructure.Xml.XsdValidator(
                                sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>())));

                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                        options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidIssuer = Issuer,
                                ValidateAudience = true,
                                ValidAudience = Audience,
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = new RsaSecurityKey(SigningRsa),
                                ValidateLifetime = false,
                                RoleClaimType = JwtClaims.Role,
                                NameClaimType = JwtClaims.UserId
                            };
                        });
                });
            });

        // Garante que o host subiu (migrations aplicadas no arranque).
        using var client = Factory.CreateClient();
        var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return _postgres.StopAsync();
    }

    /// <summary>Emite um access token de teste com as claims do Identity.</summary>
    public string IssueToken(Guid? tenantId, string role, Guid? userId = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtClaims.UserId, (userId ?? AdminUserId).ToString()),
            new(JwtClaims.Role, role),
            new(JwtClaims.EmailConfirmed, "true")
        };

        if (tenantId is not null)
            claims.Add(new(JwtClaims.TenantId, tenantId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(SigningRsa), SecurityAlgorithms.RsaSha256));

        return handler.WriteToken(token);
    }

    private sealed class FakeModuleChecker(Func<bool> isActive) : IModuleAccessChecker
    {
        public Task<bool> IsModuleActiveAsync(Guid tenantId, string moduleSlug, CancellationToken ct = default)
            => Task.FromResult(isActive());
    }

    private sealed class FakeFilialChecker(Func<bool> allowed)
        : Fiscal.Application.IntegrationServices.IFilialAccessChecker
    {
        public Task<Fiscal.Application.IntegrationServices.FilialAccess> ValidateAsync(
            Fiscal.Domain.Common.TenantId tenantId, Guid branchId, CancellationToken ct = default)
            => Task.FromResult(allowed()
                ? Fiscal.Application.IntegrationServices.FilialAccess.Allowed
                : Fiscal.Application.IntegrationServices.FilialAccess.Denied);
    }

    private sealed class FakeSefazConnectionFactory(Func<Func<HttpRequestMessage, HttpResponseMessage>> responder)
        : Fiscal.Infrastructure.Sefaz.ISefazConnectionFactory
    {
        public HttpClient Create(System.Security.Cryptography.X509Certificates.X509Certificate2 certificado)
            => new(new StubHandler(responder()), disposeHandler: true) { Timeout = TimeSpan.FromSeconds(60) };

        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
                => Task.FromResult(responder(request));
        }
    }

    private sealed class FakeNfseTransport(Func<string, string, string, (bool, string)> responder)
        : Fiscal.Infrastructure.Nfse.INfseHttpTransport
    {
        public Task<(bool HttpOk, string Corpo)> PostJsonAsync(
            string baseUrl, string path, string json, System.Security.Cryptography.X509Certificates.X509Certificate2? cert,
            CancellationToken ct)
            => Task.FromResult(responder(baseUrl, path, json));

        public Task<(bool HttpOk, string Corpo)> GetAsync(
            string url, System.Security.Cryptography.X509Certificates.X509Certificate2? cert,
            CancellationToken ct)
            => Task.FromResult(responder(url, "", ""));
    }

    private sealed class FakeXsdValidator(
        Func<bool> pularOficial,
        Fiscal.Infrastructure.Xml.XsdValidator real)
        : Fiscal.Application.Xml.IXsdValidator
    {
        public void Validar(string xml, string modo)
        {
            if (pularOficial() && string.Equals(modo, "oficial/4.00", StringComparison.OrdinalIgnoreCase))
                return;
            real.Validar(xml, modo);
        }
    }
}

[CollectionDefinition("integration")]
public sealed class IntegrationCollection : ICollectionFixture<FiscalApiFixture>;
