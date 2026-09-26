using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.AuditLog)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Entity)
            .HasColumnName("entity")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(200);

        builder.Property(x => x.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .IsRequired();

        // Consultas típicas: trilha por tenant/entidade; sem soft delete (R9).
        builder.HasIndex(x => new { x.TenantId, x.OccurredAtUtc })
            .HasDatabaseName("IX_audit_log_tenant_ocorrido");

        builder.HasIndex(x => new { x.TenantId, x.Entity })
            .HasDatabaseName("IX_audit_log_tenant_entidade");
    }
}
