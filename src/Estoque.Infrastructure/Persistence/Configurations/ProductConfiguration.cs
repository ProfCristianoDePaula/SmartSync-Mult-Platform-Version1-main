using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.Product)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.Sku)
            .HasColumnName("sku")
            .HasConversion(ValueConverters.SkuText)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Barcode)
            .HasColumnName("barcode")
            .HasConversion(ValueConverters.BarcodeText)
            .HasMaxLength(13);

        builder.Property(x => x.BrandId).HasColumnName("brand_id").HasConversion(ValueConverters.Brand);
        builder.Property(x => x.ModelId).HasColumnName("model_id").HasConversion(ValueConverters.Model);
        builder.Property(x => x.CategoryId).HasColumnName("category_id").HasConversion(ValueConverters.Category);

        builder.Property(x => x.UnitOfMeasure)
            .HasColumnName("unit_of_measure")
            .HasConversion(uom => (int)uom, value => (UnitOfMeasure)value)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        // Unicidade POR TENANT entre ativos (padrão Etapas 04/12/13).
        builder.HasIndex(x => new { x.TenantId, x.Sku })
            .HasDatabaseName("UX_products_tenant_sku_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => new { x.TenantId, x.Barcode })
            .HasDatabaseName("UX_products_tenant_barcode_active")
            .IsUnique()
            .HasFilter("\"is_active\" AND \"barcode\" IS NOT NULL");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_products_tenant_id");

        builder.HasQueryFilter("Active", p => p.IsActive);
    }
}

