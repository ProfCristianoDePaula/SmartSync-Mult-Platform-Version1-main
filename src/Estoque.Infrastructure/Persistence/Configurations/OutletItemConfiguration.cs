using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class OutletItemConfiguration : IEntityTypeConfiguration<OutletItem>
{
    public void Configure(EntityTypeBuilder<OutletItem> builder)
    {
        builder.ToTable("outlet_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => OutletItemId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasConversion(id => id.Value, value => BranchId.From(value))
            .IsRequired();

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .HasConversion(id => id.Value, value => ProductId.From(value))
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasConversion(reason => (int)reason, value => (OutletReason)value)
            .IsRequired();

        builder.Property(x => x.SuggestedDiscountPct).HasColumnName("suggested_discount_pct");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.ResolvedAtUtc).HasColumnName("resolved_at_utc");

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasConversion(q => q.Value, value => Quantity.FromValidated(value))
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        // Itens em aberto por filial/produto — dedupe da marcação.
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId })
            .HasDatabaseName("IX_outlet_items_tenant_branch_product");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_outlet_items_tenant_id");
    }
}
