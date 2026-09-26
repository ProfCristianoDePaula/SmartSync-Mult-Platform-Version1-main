using Fiscal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class AlertaFiscalConfiguration : IEntityTypeConfiguration<AlertaFiscal>
{
    public void Configure(EntityTypeBuilder<AlertaFiscal> builder)
    {
        builder.ToTable("alertas_fiscais");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.AlertaFiscal)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant);

        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Mensagem).HasColumnName("mensagem").HasMaxLength(500).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(200);
        builder.Property(x => x.Lida).HasColumnName("lida").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.LidaEm).HasColumnName("lida_em");

        builder.HasIndex(x => new { x.TenantId, x.Lida }).HasDatabaseName("IX_alertas_tenant_lida");
    }
}
