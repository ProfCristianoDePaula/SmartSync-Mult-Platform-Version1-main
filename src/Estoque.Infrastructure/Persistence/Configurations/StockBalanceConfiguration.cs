using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances");

        // Chave surrogate Guid + índice único da identidade natural
        // (tenant, produto, filial).
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").HasConversion(ValueConverters.Branch)
            .IsRequired();

        builder.Property(x => x.ProductId).HasColumnName("product_id").HasConversion(ValueConverters.Product)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasConversion(ValueConverters.QuantityNumber)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.AverageUnitCost)
            .HasColumnName("average_unit_cost")
            .HasConversion(ValueConverters.MoneyNumber)
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.BranchId })
            .HasDatabaseName("UX_stock_balances_tenant_product_branch")
            .IsUnique();

        // Invariante de domínio reforçada no banco.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_stock_balances_quantity_non_negative", "\"quantity\" >= 0"));
    }
}

