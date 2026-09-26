using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class UfFiscalConfiguration : IEntityTypeConfiguration<UfFiscal>
{
    public void Configure(EntityTypeBuilder<UfFiscal> builder)
    {
        builder.ToTable("ufs_fiscais");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.UfFiscal)
            .ValueGeneratedNever();

        builder.Property(x => x.Sigla)
            .HasColumnName("sigla")
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.CodigoIbge)
            .HasColumnName("codigo_ibge")
            .IsRequired();

        builder.Property(x => x.Nome)
            .HasColumnName("nome")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AutorizadorNFe)
            .HasColumnName("autorizador_nfe")
            .HasConversion(t => (int)t, v => (AutorizadorTipo)v)
            .IsRequired();

        builder.Property(x => x.AutorizadorNFCe)
            .HasColumnName("autorizador_nfce")
            .HasConversion(t => (int)t, v => (AutorizadorTipo)v)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        // Sigla e cUF únicos entre ativos (padrão Etapas 04/12/13 do Identity).
        builder.HasIndex(x => x.Sigla)
            .HasDatabaseName("UX_ufs_fiscais_sigla_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.CodigoIbge)
            .HasDatabaseName("UX_ufs_fiscais_cuf_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasQueryFilter("Active", u => u.IsActive);
    }
}
