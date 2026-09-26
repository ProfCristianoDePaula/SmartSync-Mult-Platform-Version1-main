using Fiscal.Application.Auditing;
using Fiscal.Application.Catalogo;
using Fiscal.Application.Certificados;
using Fiscal.Application.Emitentes;
using Fiscal.Application.IntegrationServices;
using Fiscal.Application.Repositories;
using Fiscal.Infrastructure.Catalogo;
using Fiscal.Infrastructure.Certificados;
using Fiscal.Infrastructure.Emitentes;
using Fiscal.Infrastructure.Identity;
using Fiscal.Infrastructure.Jobs;
using Fiscal.Infrastructure.Persistence;
using Fiscal.Infrastructure.Persistence.Auditing;
using Fiscal.Infrastructure.Persistence.Repositories;
using Fiscal.Infrastructure.Segredos;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fiscal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFiscalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        services.AddDbContext<FiscalDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Cliente do Identity (JWKS + gate) — seção "Identity".
        services.Configure<IdentityClientOptions>(
            configuration.GetSection(IdentityClientOptions.SectionName));
        services.AddHttpClient("identity", (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityClientOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddHttpContextAccessor();
        services.AddSingleton<IdentityApiClient>();
        services.AddSingleton<JwksKeyStore>();
        services.AddHostedService<JwksRefreshService>();
        services.AddSingleton<IModuleAccessChecker, ModuleAccessChecker>();

        // Auditoria (Fiscal-1): só metadados, nunca segredos (R4).
        services.AddScoped<IAuditLogger, AuditLogger>();

        // Catálogo fiscal (Fiscal-2): UFs + endpoints SEFAZ.
        services.Configure<SefazCatalogOptions>(
            configuration.GetSection(SefazCatalogOptions.SectionName));
        services.AddScoped<IUfFiscalRepository, UfFiscalRepository>();
        services.AddScoped<ISefazEndpointRepository, SefazEndpointRepository>();
        services.AddScoped<IUfFiscalService, UfFiscalService>();
        services.AddScoped<ISefazEndpointService, SefazEndpointService>();

        // Emitentes (Fiscal-4): perfil, configurações, concessões por filial.
        services.Configure<FiscalOptions>(
            configuration.GetSection(FiscalOptions.SectionName));
        services.AddSingleton<IFilialAccessChecker, FilialAccessChecker>();
        services.AddScoped<IEmitenteFiscalRepository, EmitenteFiscalRepository>();
        services.AddScoped<IConfiguracaoDocumentoRepository, ConfiguracaoDocumentoRepository>();
        services.AddScoped<IConcessaoRepository, ConcessaoRepository>();
        services.AddScoped<IEmitenteFiscalService, EmitenteFiscalService>();
        services.AddScoped<IConcessaoService, ConcessaoService>();
        services.AddScoped<ICertificadoReadModel, Emitentes.CertificadoReadModel>();

        // Cofre de segredos (Fiscal-5, R4): sem chave mestra não sobe em Production.
        services.Configure<FiscalSecretsOptions>(
            configuration.GetSection(FiscalSecretsOptions.SectionName));
        services.AddSingleton<ISecretProtector>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FiscalSecretsOptions>>().Value;
            var env = sp.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AesGcmSecretProtector>>();
            return SecretProtectorFactory.Build(opts, env.IsProduction(), logger);
        });
        services.AddScoped<ICertificadoRepository, CertificadoRepository>();
        services.AddScoped<IAlertaRepository, AlertaRepository>();
        services.AddScoped<ICertificadoService, CertificadoService>();
        services.AddScoped<ICertificadoResolver, CertificadoResolver>();
        services.AddScoped<ICscService, CscService>();
        services.AddScoped<IProdutoFiscalRepository, ProdutoFiscalRepository>();
        services.AddScoped<IClienteFiscalRepository, ClienteFiscalRepository>();
        services.AddScoped<INaturezaRepository, NaturezaRepository>();
        services.AddScoped<Fiscal.Application.Cadastros.IProdutoFiscalService, Cadastros.ProdutoFiscalService>();
        services.AddScoped<Fiscal.Application.Cadastros.IClienteFiscalService, Cadastros.ClienteFiscalService>();
        services.AddScoped<Fiscal.Application.Cadastros.INaturezaService, Cadastros.NaturezaService>();
        services.AddScoped<Fiscal.Application.Cadastros.IValidadorFiscal, Cadastros.ValidadorFiscal>();
        services.AddScoped<Fiscal.Application.Xml.INFeXmlBuilder, Xml.NFeXmlBuilder>();
        services.AddScoped<Fiscal.Application.Xml.IXsdValidator, Xml.XsdValidator>();
        services.AddScoped<Fiscal.Application.Xml.IXmlSigner, Xml.XmlSigner>();
        services.AddScoped<Fiscal.Application.Emissao.IEmissaoService, Emissao.EmissaoService>();
        services.AddScoped<Fiscal.Application.Emissao.IConciliacaoService, Emissao.ConciliacaoService>();
        services.AddSingleton<Emissao.SimuladorAutorizador>();
        services.AddScoped<Sefaz.SefazAutorizador>();
        services.AddScoped<Sefaz.INFeAssembler, Sefaz.NFeAssembler>();
        services.AddScoped<Sefaz.SefazSoapClient>();
        services.AddScoped<Sefaz.ISefazConnectionFactory, Sefaz.SefazConnectionFactory>();
        services.AddScoped<Sefaz.IAutorizadorSelector, Sefaz.AutorizadorSelector>();
        services.AddScoped<Nfse.DpsBuilder>();
        services.AddScoped<Fiscal.Application.Nfse.INfseProvider, Nfse.NfseNacionalProvider>();
        services.AddScoped<Nfse.INfseHttpTransport, Nfse.NfseHttpTransport>();
        services.AddScoped<Nfse.NfsePipelineAdapter>();
        services.AddHostedService<Jobs.EmissaoWorker>();

        // Arquivos (Fiscal-11): disco/volume + DANFE/DANFCE.
        services.Configure<Arquivos.ArmazenamentoOptions>(
            configuration.GetSection(Arquivos.ArmazenamentoOptions.SectionName));
        services.AddScoped<Fiscal.Application.Arquivos.IArmazenamentoFiscal, Arquivos.ArmazenamentoDisco>();
        services.AddScoped<Fiscal.Application.Arquivos.IDanfeGerador, Arquivos.DanfeGerador>();
        services.AddScoped<Arquivos.ArquivadorDocumentos>();
        services.AddScoped<Fiscal.Application.Documentos.IDocumentoFiscalRepository, Persistence.Repositories.DocumentoFiscalRepository>();
        services.AddScoped<Fiscal.Application.Documentos.ISerieNumeracaoService, Persistence.Repositories.SerieNumeracaoService>();
        services.AddScoped<IAlertaService, AlertaService>();
        services.AddScoped<ValidadeCertificadoService>();
        services.AddHostedService<CertificadoExpiryWorker>();

        // NFS-e municipal (Fiscal-3): importação + situação + parametrização.
        services.AddScoped<IMunicipioRepository, MunicipioRepository>();
        services.AddScoped<INfseMunicipioConfigRepository, NfseMunicipioConfigRepository>();
        services.AddScoped<INfseAmbienteRepository, NfseAmbienteRepository>();
        services.AddScoped<Fiscal.Application.Nfse.INfseMunicipioService, Nfse.NfseMunicipioService>();
        services.AddScoped<Fiscal.Application.Nfse.INfseParametrizacaoClient, Nfse.NfseParametrizacaoClient>();
        services.AddHttpClient("nfse-parametros", client => client.Timeout = TimeSpan.FromSeconds(15));

        return services;
    }
}
