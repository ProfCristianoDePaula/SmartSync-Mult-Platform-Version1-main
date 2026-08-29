using System.Security.Claims;
using Estoque.Api;
using Estoque.Api.Auth;
using Estoque.Api.Observability;
using Estoque.Api.OpenApi;
using Estoque.Application;
using Estoque.Domain.Common;
using Estoque.Infrastructure;
using Estoque.Infrastructure.Identity;
using Estoque.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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

    // Serilog estruturado (padrão Etapa 07 do Identity): Correlation ID.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            "logs/estoque-api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14)
    );

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<AuthorizeOperationTransformer>();
    });
    builder.Services.AddEstoqueApplication();
    builder.Services.AddEstoqueInfrastructure(builder.Configuration);
    builder.Services.AddControllers();

    // ---------------------------------------------------------------------------
    // Autorização (padrões auditados):
    //  - policy "tenant": exige claim tenant_id (SuperAdmin global NÃO opera
    //    Estoque na v1 — decisão da Etapa 23);
    //  - policy "module-estoque": gate de contratação via Identity (/me/modules,
    //    cacheado; fail-closed).
    // ---------------------------------------------------------------------------
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("tenant", policy =>
            policy.RequireClaim(JwtClaims.TenantId));

        options.AddPolicy("module-estoque", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ModuleActiveRequirement(
                builder.Configuration.GetSection(IdentityClientOptions.SectionName)
                           .Get<IdentityClientOptions>()?.ModuleSlug ?? "estoque"));
        });
    });
    builder.Services.AddSingleton<IAuthorizationHandler, ModuleActiveHandler>();

    // Cache distribuído opcional (padrão do Identity): IMemoryCache padrão.
    builder.Services.AddMemoryCache();

    // Health checks (readiness com o Postgres próprio).
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "estoque-postgres",
            timeout: TimeSpan.FromSeconds(5));

    // Métricas Prometheus (/metrics) — mesmo pipeline do Identity.
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter());

    // Rate limiting no upload pesado de XML (análogo ao rate limit de auth).
    var xmlImportLimit = builder.Configuration.GetSection("RateLimiting:XmlImport")
        .Get<RateLimiterSection>() ?? new RateLimiterSection { PermitLimit = 10, WindowSeconds = 60 };

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("xml-import", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = xmlImportLimit.PermitLimit,
                    Window = TimeSpan.FromSeconds(xmlImportLimit.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    // ---------------------------------------------------------------------------
    // Autenticação JWT RS256 emitido pelo IDENTITY — chave pública via JWKS
    // (GET /api/auth/jwks), armazenada em JwksKeyStore (singleton refrescado por
    // hosted service). Issuer/Audience canônicos compartilhados via env.
    // ---------------------------------------------------------------------------
    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"];

    // Capturada no closure do resolver; atribuída após o Build() (padrão do
    // SigningKeyProvider do Identity).
    JwksKeyStore? keyStore = null;

    var authenticationBuilder = builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // claims literais do Identity (user_id/tenant_id/role)
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = (_, _, _, _) =>
                {
                    var keys = keyStore?.GetKeys() ?? [];
                    return keys.Count == 0
                        ? throw new InvalidOperationException(
                            "JWKS do Identity ainda indisponível — tente novamente em instantes.")
                        : keys;
                },
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = JwtClaims.Role,
                NameClaimType = JwtClaims.UserId
            };
        });

    var app = builder.Build();
    keyStore = app.Services.GetRequiredService<JwksKeyStore>();

    // OpenAPI sempre exposto (contrato consumível pelos demais serviços);
    // UI Scalar apenas em Development (padrão Etapa 09/10).
    app.MapOpenApi();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Estoque API");
            options.AddPreferredSecuritySchemes(["Bearer"]);
            options.AddHttpAuthentication("Bearer", scheme => { });
        });
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();

    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Migrations aplicadas automaticamente (padrão IdentitySeeder).
    await EstoqueDbInitializer.InitializeAsync(app.Services);

    app.MapHealthChecks("/api/health").AllowAnonymous();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Estoque.Api terminou de forma inesperada.");
    throw; // fail-fast: o orquestrador (compose/k8s) reinicia; engolir aqui
           // deixa hosts de teste com "server has not been started".
}
finally
{
    Log.CloseAndFlush();
}



/// <summary>Acesso ao Program para o WebApplicationFactory dos testes.</summary>
public partial class Program { }


