using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Estoque.Application.IntegrationServices;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

using Xunit;
namespace Estoque.Tests;

/// <summary>
/// Fixture compartilhada (collection "integration") — Postgres 100% real via
/// Testcontainers + host de teste com JWT assinado por chave RSA da própria
/// fixture e checkers do Identity substituídos por fakes permissivos
/// (o comportamento HTTP do Identity é coberto pela suíte do Identity).
/// </summary>
public sealed class EstoqueApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("estoque")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public RSA SigningRsa { get; } = RSA.Create(2048);
    public const string Issuer = "https://identity.test";
    public const string Audience = "estoque-tests";
    public static readonly Guid TenantA = Guid.NewGuid();
    public static readonly Guid BranchA = Guid.NewGuid();
    public static Guid AdminUserId { get; } = Guid.NewGuid();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                // UseSetting tem precedência sobre appsettings/user-secrets/env.
                builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
                builder.UseSetting("Jwt:Issuer", Issuer);
                builder.UseSetting("Jwt:Audience", Audience);
                builder.UseSetting("Identity:BaseUrl", "http://localhost:1"); // nunca chamado (fakes)

                // Fakes dos gates de integração + chave de assinatura dos testes.
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IModuleAccessChecker>(new FakeModuleChecker());
                    services.AddSingleton<IFilialAccessChecker>(new FakeBranchChecker());

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
    public string IssueToken(Guid tenantId, string role, Guid? userId = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new(JwtClaims.UserId, (userId ?? AdminUserId).ToString()),
            new(JwtClaims.TenantId, tenantId.ToString()),
            new(JwtClaims.Role, role),
            new(JwtClaims.EmailConfirmed, "true")
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(SigningRsa), SecurityAlgorithms.RsaSha256));

        return handler.WriteToken(token);
    }

    private sealed class FakeModuleChecker : IModuleAccessChecker
    {
        public Task<bool> IsModuleActiveAsync(Guid tenantId, string moduleSlug, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class FakeBranchChecker : IFilialAccessChecker
    {
        public Task<FilialAccess> ValidateAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default)
            => Task.FromResult(FilialAccess.Allowed);
    }
}

[CollectionDefinition("integration")]
public sealed class IntegrationCollection : ICollectionFixture<EstoqueApiFixture>;



