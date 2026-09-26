using Fiscal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class MunicipioConfiguration : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> builder)
    {
        builder.ToTable("municipios");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.Municipio)
            .ValueGeneratedNever();

        builder.Property(x => x.CodigoIbge)
            .HasColumnName("codigo_ibge")
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(x => x.Nome)
            .HasColumnName("nome")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Uf)
            .HasColumnName("uf")
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => x.CodigoIbge)
            .HasDatabaseName("UX_municipios_ibge_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.Uf).HasDatabaseName("IX_municipios_uf");

        builder.HasQueryFilter("Active", m => m.IsActive);
    }
}
