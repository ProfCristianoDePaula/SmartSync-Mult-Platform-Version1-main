using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class SerieNumeracaoConfiguration : IEntityTypeConfiguration<SerieNumeracao>
{
    public void Configure(EntityTypeBuilder<SerieNumeracao> builder)
    {
        builder.ToTable("series_numeracao");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasConversion(ValueConverters.SerieNumeracao).ValueGeneratedNever();
        builder.Property(x => x.EmitenteId).HasColumnName("emitente_id").HasConversion(ValueConverters.EmitenteFiscal).IsRequired();
        builder.Property(x => x.Modelo).HasColumnName("modelo").IsRequired();
        builder.Property(x => x.Serie).HasColumnName("serie").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Ambiente).HasColumnName("ambiente").HasConversion(a => (int)a, v => (AmbienteFiscal)v).IsRequired();
        builder.Property(x => x.UltimoNumero).HasColumnName("ultimo_numero").IsRequired();

        builder.HasIndex(x => new { x.EmitenteId, x.Modelo, x.Serie, x.Ambiente })
            .HasDatabaseName("UX_series_emitente_modelo_serie_amb")
            .IsUnique();
    }
}
