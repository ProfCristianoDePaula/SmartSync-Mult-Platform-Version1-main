using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class EventoFiscalConfiguration : IEntityTypeConfiguration<EventoFiscal>
{
    public void Configure(EntityTypeBuilder<EventoFiscal> builder)
    {
        builder.ToTable("eventos_fiscais");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasConversion(ValueConverters.EventoFiscal).ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(ValueConverters.Tenant).IsRequired();
        builder.Property(x => x.DocumentoId).HasColumnName("documento_id").HasConversion(ValueConverters.DocumentoFiscal).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion(t => (int)t, v => (TipoEventoFiscal)v).IsRequired();
        builder.Property(x => x.CodigoEvento).HasColumnName("codigo_evento").HasMaxLength(20);
        builder.Property(x => x.Justificativa).HasColumnName("justificativa").HasMaxLength(1000);
        builder.Property(x => x.Protocolo).HasColumnName("protocolo").HasMaxLength(50);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(x => x.DocumentoId).HasDatabaseName("IX_eventos_documento");
        builder.HasIndex(x => new { x.TenantId, x.Tipo }).HasDatabaseName("IX_eventos_tenant_tipo");
    }
}
