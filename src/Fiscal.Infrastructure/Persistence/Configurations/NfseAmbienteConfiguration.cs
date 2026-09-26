using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class NfseAmbienteConfiguration : IEntityTypeConfiguration<NfseAmbiente>
{
    public void Configure(EntityTypeBuilder<NfseAmbiente> builder)
    {
        builder.ToTable("nfse_ambientes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.NfseAmbiente)
            .ValueGeneratedNever();

        builder.Property(x => x.CodigoIbge)
            .HasColumnName("codigo_ibge")
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(x => x.Modo)
            .HasColumnName("modo")
            .HasConversion(m => (int)m, v => (ModoEmissaoNfse)v)
            .IsRequired();

        builder.Property(x => x.Ambiente)
            .HasColumnName("ambiente")
            .HasConversion(a => (int)a, v => (AmbienteFiscal)v)
            .IsRequired();

        builder.Property(x => x.BaseUrlSefin).HasColumnName("base_url_sefin").HasMaxLength(500);
        builder.Property(x => x.BaseUrlAdn).HasColumnName("base_url_adn").HasMaxLength(500);
        builder.Property(x => x.BaseUrlParametros).HasColumnName("base_url_parametros").HasMaxLength(500);

        builder.Property(x => x.Verificado)
            .HasColumnName("verificado")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => new { x.CodigoIbge, x.Modo, x.Ambiente })
            .HasDatabaseName("UX_nfse_ambientes_ibge_modo_amb_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasQueryFilter("Active", a => a.IsActive);
    }
}
