using Identity.Api.Observability;
using Identity.Api.OpenApi;
using Identity.Application;
using Identity.Application.Auth;
using Identity.Application.Observability;
using Identity.Domain.Common;
using Identity.Infrastructure;
using Identity.Infrastructure.Auth;
using Identity.Infrastructure.Seed;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog estruturado (Etapa 07): logs estruturados com Correlation ID.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            "logs/identity-api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14)
    );

    builder.Services.AddOpenApi(options =>
    {
        // Etapa 09: documentação OpenAPI 3.1 (nativa) com esquema Bearer (JWT).
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<AuthorizeOperationTransformer>();
    });
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddAuthorization(options =>
    {
        // Ações que exigem conta ativa (e-mail confirmado), tarefa 5 da Etapa 06.
        options.AddPolicy("email-confirmed", policy =>
            policy.RequireClaim(JwtClaims.EmailConfirmed, "true"));
    });

    // Cache (Etapa 07): IMemoryCache é padrão; Redis/Valkey (distribuído) é
    // registrado somente quando Cache:Mode = "redis" e há connection string
    // (ex.: docker-compose com serviço redis/valkey).
    builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
    builder.Services.AddMemoryCache();
    if (builder.Configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>()?.RedisEnabled == true)
    {
        var redisConnection = builder.Configuration.GetSection(CacheOptions.SectionName)["RedisConnectionString"]!;
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
        });
    }

    // Health checks (Etapa 07): liveness (sempre ok) e readiness com o Postgres.
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgres",
            timeout: TimeSpan.FromSeconds(5));

    // Métricas (Etapa 07): pipeline OpenTelemetry expondo métricas do ASP.NET
    // Core + do Identity (.NET 10 emite no analyzer "Microsoft.AspNetCore.Identity").
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Identity")
            .AddPrometheusExporter());

    // Rate limiting nativo (Etapa 07): política fixa por IP para os endpoints
    // sensíveis de autenticação (proteção contra força bruta / spray de senha).
    // Limites vêm da seção "RateLimiting" do config (ex.: testes sobem o limite).
    var rateLimitsLogin = builder.Configuration
        .GetSection("RateLimiting:Login")
        .Get<RateLimiterSection>()
        ?? new RateLimiterSection { PermitLimit = 5, WindowSeconds = 60 };
    var rateLimitsRefresh = builder.Configuration
        .GetSection("RateLimiting:Refresh")
        .Get<RateLimiterSection>()
        ?? new RateLimiterSection { PermitLimit = 10, WindowSeconds = 60 };
    var rateLimitsRecovery = builder.Configuration
        .GetSection("RateLimiting:PasswordRecovery")
        .Get<RateLimiterSection>()
        ?? new RateLimiterSection { PermitLimit = 3, WindowSeconds = 60 };
    var rateLimitsRegister = builder.Configuration
        .GetSection("RateLimiting:Register")
        .Get<RateLimiterSection>()
        ?? new RateLimiterSection { PermitLimit = 10, WindowSeconds = 60 };
    var rateLimitingWindow = TimeSpan.FromSeconds(rateLimitsLogin.WindowSeconds);

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth-login", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitsLogin.PermitLimit,
                    Window = rateLimitingWindow,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.AddPolicy("auth-refresh", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitsRefresh.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitsRefresh.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.AddPolicy("auth-recovery", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitsRecovery.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitsRecovery.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        options.AddPolicy("auth-register", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitsRegister.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitsRegister.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    var jwtOptions = builder.Configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
        ?? new JwtOptions();

    var oauthOptions = builder.Configuration
        .GetSection(OAuthOptions.SectionName)
        .Get<OAuthOptions>()
        ?? new OAuthOptions();

    // Resolvida uma única vez (singleton, compartilhada com a emissão) e capturada
    // para o validator usar a MESMA chave pública usada na emissão.
    SigningKeyProvider? provider = null;

    var authenticationBuilder = builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = (_, _, _, _) => [provider!.PublicKey],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

    // Login social (Etapa 05) — exclusivo para a role Client. Credenciais criadas
    // manualmente nos consoles do Google Cloud / Facebook for Developers e
    // fornecidas via configuração (env vars OAuth__Google__ClientId etc.).
    // O sign-in intermediário usa o cookie externo do Identity: o provider repassa
    // apenas o ticket autenticado de volta ao callback (sem sessão persistente).
    if (!string.IsNullOrWhiteSpace(oauthOptions.Google.ClientId) &&
        !string.IsNullOrWhiteSpace(oauthOptions.Google.ClientSecret))
    {
        authenticationBuilder.AddGoogle(options =>
        {
            options.ClientId = oauthOptions.Google.ClientId;
            options.ClientSecret = oauthOptions.Google.ClientSecret;
            options.SignInScheme = IdentityConstants.ExternalScheme;
        });
    }

    if (!string.IsNullOrWhiteSpace(oauthOptions.Facebook.ClientId) &&
        !string.IsNullOrWhiteSpace(oauthOptions.Facebook.ClientSecret))
    {
        authenticationBuilder.AddFacebook(options =>
        {
            options.ClientId = oauthOptions.Facebook.ClientId;
            options.ClientSecret = oauthOptions.Facebook.ClientSecret;
            options.SignInScheme = IdentityConstants.ExternalScheme;
        });
    }

    // Cookie de curta duração usado apenas durante o fluxo OAuth externo
    // (NÃO é usado para sessão do usuário — a API segue com JWT).
    authenticationBuilder.AddCookie(IdentityConstants.ExternalScheme, options =>
    {
        options.Cookie.Name = "identity.external";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

    var app = builder.Build();

    provider = app.Services.GetRequiredService<SigningKeyProvider>();

    // OpenAPI (Etapa 09): o documento /openapi/v1.json fica sempre exposto para
    // que os demais microsserviços possam consumir o contrato; a UI (Scalar,
    // Etapa 10 — substituta do Swagger) fica restrita ao Development.
    app.MapOpenApi();

    if (app.Environment.IsDevelopment())
    {
        // UI Scalar (Etapa 10) para testes manuais. O esquema "Bearer" já está
        // declarado no documento OpenAPI por BearerSecuritySchemeTransformer,
        // então o Scalar renderiza o campo para colar o JWT nos endpoints
        // protegidos (send-sms-code/confirm-phone).
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Identity API");
            options.AddPreferredSecuritySchemes(["Bearer"]);
            options.AddHttpAuthentication("Bearer", scheme => { });
        });
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();

    // Exposição das métricas no formato Prometheus (/metrics).
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    await IdentitySeeder.SeedAsync(app.Services);
    await DbSeeder.SeedAsync(app.Services);

    app.MapHealthChecks("/api/health").AllowAnonymous();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity.Api terminou de forma inesperada.");
}
finally
{
    Log.CloseAndFlush();
}