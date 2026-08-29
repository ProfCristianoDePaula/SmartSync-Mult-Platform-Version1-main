using Identity.Application.Auth;
using Identity.Application.Branches;
using Identity.Application.Modules;
using Identity.Application.Notifications;
using Identity.Application.Plans;
using Identity.Application.RoleCatalog;
using Identity.Application.Tenants;
using Identity.Application.TenantModules;
using Identity.Application.Uniqueness;
using Identity.Infrastructure.Auth;
using Identity.Infrastructure.Branches;
using Identity.Infrastructure.Modules;
using Identity.Infrastructure.Notifications;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Identity.Infrastructure.Plans;
using Identity.Infrastructure.RoleCatalog;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Tenants;
using Identity.Infrastructure.TenantModules;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // E-mail único POR TENANT (índice composto em (tenant_id, email)).
                // A unicidade global fica a cargo das regras de negócio da etapa 04.
                options.User.RequireUniqueEmail = false;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<ApplicationRole>()
            .AddRoleManager<RoleManager<ApplicationRole>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddErrorDescriber<PtBrIdentityErrorDescriber>()
            // Habilita os token providers nativos (e-mail, telefone/2FA) usados
            // na confirmação de e-mail e validação de celular (Etapa 06).
            .AddDefaultTokenProviders();

        // Token de reset de senha por e-mail e de confirmação de e-mail usam o
        // MESMO provider padrão (DataProtectorTokenProvider). Por padrão o
        // Identity permite 1 dia; encurtamos para 30 min (decisão da Etapa 11).
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(30));

        // Chaves do DataProtection PERSISTIDAS (ex.: volume em Docker). Sem isso,
        // tokens de confirmação/reset emitidos antes de um restart ficam inválidos,
        // pois o Identity gera novas chaves a cada subida do processo.
        var dataProtectionKeyPath = configuration["DataProtection:KeyPath"] ?? "keys/dataprotection";
        services
            .AddDataProtection()
            .SetApplicationName("Identity")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(dataProtectionKeyPath)));

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<SigningKeyProvider>();
        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISocialAuthService, SocialAuthService>();

        // Notificações (Etapa 06): e-mail via SMTP (MailKit) e SMS via Twilio.
        // Em dev, se a infraestrutura real não estiver configurada, usamos
        // implementações de apenas-log (dev), ajustadas via options.
        services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SmsOptions>(
            configuration.GetSection(SmsOptions.SectionName));
        services.AddHttpClient();

        services.AddScoped<IEmailSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
            return options.EnableSmtp
                ? provider.GetRequiredService<SmtpEmailSender>()
                : provider.GetRequiredService<LogEmailSender>();
        });
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<LogEmailSender>();

        services.AddScoped<ISmsSender>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
            return options.Provider.Equals("twilio", StringComparison.OrdinalIgnoreCase)
                   && options.TwilioConfigured
                ? provider.GetRequiredService<TwilioSmsSender>()
                : provider.GetRequiredService<LogSmsSender>();
        });
        services.AddScoped<TwilioSmsSender>();
        services.AddScoped<LogSmsSender>();

        services.AddScoped<IUniquenessChecker, UniquenessChecker>();
        services.AddScoped<TenantUniquenessValidator>();
        services.AddScoped<UserUniquenessValidator>();

        services.AddScoped<IRoleService, RoleService>();

        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IPlanLimitResolver, PlanLimitResolver>();

        services.AddScoped<ITenantService, TenantService>();

        services.AddScoped<IBranchService, BranchService>();

        services.AddScoped<IModuleService, ModuleService>();

        services.AddScoped<ITenantModuleService, TenantModuleService>();

        return services;
    }
}