using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class NfseMunicipioConfigConfiguration : IEntityTypeConfiguration<NfseMunicipioConfig>
{
    public void Configure(EntityTypeBuilder<NfseMunicipioConfig> builder)
    {
        builder.ToTable("nfse_municipio_configs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.NfseMunicipioConfig)
            .ValueGeneratedNever();

        builder.Property(x => x.CodigoIbge)
            .HasColumnName("codigo_ibge")
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(x => x.Modo)
            .HasColumnName("modo")
            .HasConversion(m => (int)m, v => (ModoEmissaoNfse)v)
            .IsRequired();

        builder.Property(x => x.Fonte)
            .HasColumnName("fonte")
            .HasMaxLength(500);

        builder.Property(x => x.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();

        builder.Property(x => x.SobrescritoManual)
            .HasColumnName("sobrescrito_manual")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Observacao)
            .HasColumnName("observacao")
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => x.CodigoIbge)
            .HasDatabaseName("UX_nfse_municipio_configs_ibge_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasQueryFilter("Active", c => c.IsActive);
    }
}
