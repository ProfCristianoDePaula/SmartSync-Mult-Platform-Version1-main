using Identity.Application.Notifications;
using Identity.Tests.Lab;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Identity.Tests;

/// <summary>
/// WebApplicationFactory da aplicação real (Identity.Api) apontando para um
/// Postgres real provisionado via Testcontainers (Etapa 09). Substitui os
/// senders de e-mail/SMS por capturadores em memória para permitir fluxos
/// end-to-end de confirmação sem infraestrutura externa.
///
/// É a collection fixture da coleção "integration": um único Postgres sobe para
/// toda a suíte. Dados criados nos testes usam valores únicos, então a
/// unicidade/isolamento é preservada entre testes.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; private set; } = null!;

    public CapturingEmailSender EmailSender { get; } = new();
    public CapturingSmsSender SmsSender { get; } = new();

    /// <summary>Client HTTP do TestServer (sem redirects automáticos).</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    Task IAsyncLifetime.InitializeAsync() => InitializePostgresAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposePostgresAsync();

    /// <summary>Não usamos o dispose base do WebApplicationFactory aqui; a Jornada
    /// do container é tratada por <see cref="DisposePostgresAsync"/>.</summary>
    public override ValueTask DisposeAsync() => base.DisposeAsync();

    public async Task InitializePostgresAsync()
    {
        Postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithDatabase("identity_tests")
            .Build();
        await Postgres.StartAsync();
    }

    public async Task DisposePostgresAsync()
    {
        await Postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", Postgres.GetConnectionString());

        // Rate limiting: sobe o limite para os testes não sofrerem 429 (todos
        // os requests de teste vêm do mesmo IP no TestServer).
        builder.UseSetting("RateLimiting:Login:PermitLimit", "1000");
        builder.UseSetting("RateLimiting:Login:WindowSeconds", "3600");
        builder.UseSetting("RateLimiting:Refresh:PermitLimit", "1000");
        builder.UseSetting("RateLimiting:Refresh:WindowSeconds", "3600");
        builder.UseSetting("RateLimiting:PasswordRecovery:PermitLimit", "1000");
        builder.UseSetting("RateLimiting:PasswordRecovery:WindowSeconds", "3600");
        builder.UseSetting("RateLimiting:Register:PermitLimit", "1000");
        builder.UseSetting("RateLimiting:Register:WindowSeconds", "3600");

        // Chave de assinatura isolada por factory (evita colisão no shared dir).
        builder.UseSetting("Jwt:SigningKeyPath",
            Path.Combine(Path.GetTempPath(), $"identity-test-{Guid.NewGuid():N}.pem"));

        // OAuth desabilitado nos testes (sem credenciais configuradas).
        builder.UseSetting("OAuth:Google:ClientId", "");
        builder.UseSetting("OAuth:Google:ClientSecret", "");
        builder.UseSetting("OAuth:Facebook:ClientId", "");
        builder.UseSetting("OAuth:Facebook:ClientSecret", "");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddScoped<IEmailSender>(_ => EmailSender);

            services.RemoveAll<ISmsSender>();
            services.AddScoped<ISmsSender>(_ => SmsSender);
        });
    }
}

/// <summary>
/// Coleção de integração que compartilha um único <see cref="IdentityApiFactory"/>
/// (e, portanto, um único Postgres via Testcontainers) entre todas as classes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "integration";
}