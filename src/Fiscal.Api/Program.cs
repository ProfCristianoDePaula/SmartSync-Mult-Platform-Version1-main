using Fiscal.Api.Auth;
using Fiscal.Api.ExceptionHandling;
using Fiscal.Api.Observability;
using Fiscal.Api.OpenApi;
using Fiscal.Application;
using Fiscal.Domain.Common;
using Fiscal.Infrastructure;
using Fiscal.Infrastructure.Identity;
using Fiscal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog estruturado (padrão Identity/Estoque): Correlation ID.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            "logs/fiscal-api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14)
    );

    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<AuthorizeOperationTransformer>();
    });
    builder.Services.AddFiscalApplication();
    builder.Services.AddFiscalInfrastructure(builder.Configuration);
    builder.Services.AddControllers();

    // ---------------------------------------------------------------------------
    // Autorização:
    //  - policy "tenant": exige claim tenant_id (SuperAdmin global NÃO opera
    //    o Fiscal — R5, mesma decisão do Estoque);
    //  - policy "module-fiscal": gate de contratação via Identity (/me/modules,
    //    cacheado; fail-closed, R10).
    // ---------------------------------------------------------------------------
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("tenant", policy =>
            policy.RequireClaim(JwtClaims.TenantId));

        options.AddPolicy("module-fiscal", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new ModuleActiveRequirement(
                builder.Configuration.GetSection(IdentityClientOptions.SectionName)
                           .Get<IdentityClientOptions>()?.ModuleSlug ?? "fiscal"));
        });
    });
    builder.Services.AddSingleton<IAuthorizationHandler, ModuleActiveHandler>();

    // Handler global → ProblemDetails (R13, pt-BR, sem segredos em resposta).
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Cache distribuído opcional (padrão do Identity): IMemoryCache padrão.
    builder.Services.AddMemoryCache();

    // Health checks (readiness com o Postgres próprio).
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "fiscal-postgres",
            timeout: TimeSpan.FromSeconds(5));

    // Métricas Prometheus (/metrics) — mesmo pipeline do Identity/Estoque.
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter());

    // ---------------------------------------------------------------------------
    // Autenticação JWT RS256 emitido pelo IDENTITY — chave pública via JWKS
    // (GET /api/auth/jwks), armazenada em JwksKeyStore (singleton refrescado por
    // hosted service). Issuer/Audience canônicos compartilhados via env.
    // ---------------------------------------------------------------------------
    var jwtIssuer = builder.Configuration["Jwt:Issuer"];
    var jwtAudience = builder.Configuration["Jwt:Audience"];

    // Capturada no closure do resolver; atribuída após o Build().
    JwksKeyStore? keyStore = null;

    builder.Services
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
    // UI Scalar apenas em Development.
    app.MapOpenApi();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Fiscal API");
            options.AddPreferredSecuritySchemes(["Bearer"]);
            options.AddHttpAuthentication("Bearer", scheme => { });
        });
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();

    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.UseExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();

    // Migrations aplicadas automaticamente (padrão IdentitySeeder).
    await FiscalDbInitializer.InitializeAsync(app.Services);

    app.MapHealthChecks("/api/health").AllowAnonymous();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fiscal.Api terminou de forma inesperada.");
    throw; // fail-fast: o orquestrador (compose/k8s) reinicia; engolir aqui
           // deixa hosts de teste com "server has not been started".
}
finally
{
    Log.CloseAndFlush();
}



/// <summary>Acesso ao Program para o WebApplicationFactory dos testes.</summary>
public partial class Program { }
