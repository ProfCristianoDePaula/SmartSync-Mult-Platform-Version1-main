using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Estoque.Domain.Common.AlertId, Guid>(v => v.Value, v => Estoque.Domain.Common.AlertId.From(v)))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").HasConversion(ValueConverters.Branch);

        builder.Property(x => x.ProductId).HasColumnName("product_id").HasConversion(ValueConverters.Product);

        builder.Property(x => x.LotIdRef).HasColumnName("lot_id").HasConversion(ValueConverters.Lot);

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion(type => (int)type, value => (AlertType)value)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasConversion(severity => (int)severity, value => (AlertSeverity)value)
            .IsRequired();

        builder.Property(x => x.Message).HasColumnName("message").HasMaxLength(1000).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.GeneratedOn).HasColumnName("generated_on");
        builder.Property(x => x.AcknowledgedAtUtc).HasColumnName("acknowledged_at_utc");
        builder.Property(x => x.AcknowledgedByUserId).HasColumnName("acknowledged_by_user_id");

        // Dedupe dos geradores: (tenant, tipo, produto, filial, dia).
        builder.HasIndex(x => new { x.TenantId, x.Type, x.ProductId, x.BranchId, x.GeneratedOn })
            .HasDatabaseName("UX_alerts_dedupe")
            .IsUnique()
            .HasFilter("\"product_id\" IS NOT NULL AND \"branch_id\" IS NOT NULL");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAtUtc })
            .HasDatabaseName("IX_alerts_tenant_created_at");
    }
}



