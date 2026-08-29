using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class StockRuleConfiguration : IEntityTypeConfiguration<StockRule>
{
    public void Configure(EntityTypeBuilder<StockRule> builder)
    {
        builder.ToTable("stock_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.StockRule)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasConversion(ValueConverters.Product)
            .IsRequired();

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasConversion(ValueConverters.Branch);

        builder.Property(x => x.MinimumQuantity)
            .HasColumnName("minimum_quantity")
            .HasConversion(ValueConverters.QuantityNumber)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.ReorderPoint)
            .HasColumnName("reorder_point")
            .HasConversion(ValueConverters.QuantityNumber)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.LeadTimeDays).HasColumnName("lead_time_days");
        builder.Property(x => x.IsActive)
            .HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Uma regra por (tenant, produto, filial) — filial nula = default do tenant.
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.BranchId })
            .HasDatabaseName("UX_stock_rules_tenant_product_branch_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_stock_rules_tenant_id");

        builder.HasQueryFilter("Active", r => r.IsActive);
    }
}

