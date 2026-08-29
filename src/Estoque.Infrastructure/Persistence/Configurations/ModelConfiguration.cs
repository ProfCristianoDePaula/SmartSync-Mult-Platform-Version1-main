using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> builder)
    {
        builder.ToTable("models");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => ModelId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.BrandId)
            .HasColumnName("brand_id")
            .HasConversion(id => id.Value, value => BrandId.From(value))
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.BrandId, x.Name })
            .HasDatabaseName("UX_models_tenant_brand_name_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_models_tenant_id");

        builder.HasQueryFilter("Active", m => m.IsActive);
    }
}
