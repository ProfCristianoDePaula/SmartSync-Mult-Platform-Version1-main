using Fiscal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class NfseImportRunConfiguration : IEntityTypeConfiguration<NfseImportRun>
{
    public void Configure(EntityTypeBuilder<NfseImportRun> builder)
    {
        builder.ToTable("nfse_import_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.NfseImportRun)
            .ValueGeneratedNever();

        builder.Property(x => x.OcorridoEm).HasColumnName("ocorrido_em").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ArquivoHash).HasColumnName("arquivo_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Total).HasColumnName("total").IsRequired();
        builder.Property(x => x.Criados).HasColumnName("criados").IsRequired();
        builder.Property(x => x.Atualizados).HasColumnName("atualizados").IsRequired();
        builder.Property(x => x.IgnoradosManuais).HasColumnName("ignorados_manuais").IsRequired();
        builder.Property(x => x.Rejeitados).HasColumnName("rejeitados").IsRequired();

        builder.HasIndex(x => x.OcorridoEm).HasDatabaseName("IX_nfse_import_runs_ocorrido");
    }
}
