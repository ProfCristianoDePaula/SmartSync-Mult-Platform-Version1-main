using Fiscal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence;

public sealed class FiscalDbContext : DbContext
{
    public FiscalDbContext(DbContextOptions<FiscalDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UfFiscal> UfsFiscais => Set<UfFiscal>();
    public DbSet<SefazEndpoint> SefazEndpoints => Set<SefazEndpoint>();
    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<NfseMunicipioConfig> NfseMunicipioConfigs => Set<NfseMunicipioConfig>();
    public DbSet<NfseAmbiente> NfseAmbientes => Set<NfseAmbiente>();
    public DbSet<NfseImportRun> NfseImportRuns => Set<NfseImportRun>();
    public DbSet<EmitenteFiscal> EmitentesFiscais => Set<EmitenteFiscal>();
    public DbSet<ConfiguracaoDocumento> ConfiguracoesDocumento => Set<ConfiguracaoDocumento>();
    public DbSet<ConcessaoUnidade> ConcessoesUnidade => Set<ConcessaoUnidade>();
    public DbSet<CertificadoDigital> CertificadosDigitais => Set<CertificadoDigital>();
    public DbSet<AlertaFiscal> AlertasFiscais => Set<AlertaFiscal>();
    public DbSet<ProdutoFiscal> ProdutosFiscais => Set<ProdutoFiscal>();
    public DbSet<ClienteFiscal> ClientesFiscais => Set<ClienteFiscal>();
    public DbSet<NaturezaOperacao> NaturezasOperacao => Set<NaturezaOperacao>();
    public DbSet<DocumentoFiscal> DocumentosFiscais => Set<DocumentoFiscal>();
    public DbSet<EventoFiscal> EventosFiscais => Set<EventoFiscal>();
    public DbSet<SerieNumeracao> SeriesNumeracao => Set<SerieNumeracao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FiscalDbContext).Assembly);
    }
}
