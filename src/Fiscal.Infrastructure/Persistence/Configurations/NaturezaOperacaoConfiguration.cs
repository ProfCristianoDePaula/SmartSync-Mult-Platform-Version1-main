using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class NaturezaOperacaoConfiguration : IEntityTypeConfiguration<NaturezaOperacao>
{
    public void Configure(EntityTypeBuilder<NaturezaOperacao> builder)
    {
        builder.ToTable("naturezas_operacao");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasConversion(ValueConverters.NaturezaOperacao).ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(ValueConverters.Tenant).IsRequired();
        builder.Property(x => x.Codigo).HasColumnName("codigo").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(255).IsRequired();
        builder.Property(x => x.TipoOperacao).HasColumnName("tipo_operacao").HasConversion(t => (int)t, v => (TipoOperacaoFiscal)v).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.Codigo })
            .HasDatabaseName("UX_naturezas_tenant_codigo_active")
            .IsUnique().HasFilter("\"is_active\"");

        builder.HasQueryFilter("Active", n => n.IsActive);
    }
}
