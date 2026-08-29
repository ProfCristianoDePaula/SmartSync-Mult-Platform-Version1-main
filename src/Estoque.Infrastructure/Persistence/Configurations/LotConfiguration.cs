using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.Lot)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasConversion(ValueConverters.Branch)
            .IsRequired();

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasConversion(ValueConverters.Product)
            .IsRequired();

        builder.Property(x => x.SupplierId)
            .HasColumnName("supplier_id")
            .HasConversion(ValueConverters.Supplier);

        builder.Property(x => x.Number)
            .HasColumnName("number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ExpiresOn).HasColumnName("expires_on").IsRequired();

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasConversion(ValueConverters.QuantityNumber)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        // Número do lote único por (tenant, produto, filial) entre ativos.
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId, x.Number })
            .HasDatabaseName("UX_lots_tenant_branch_product_number_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        // Varredura de validade (job diário).
        builder.HasIndex(x => new { x.TenantId, x.ExpiresOn })
            .HasDatabaseName("IX_lots_tenant_expires_on");

        builder.HasQueryFilter("Active", l => l.IsActive);
    }
}

