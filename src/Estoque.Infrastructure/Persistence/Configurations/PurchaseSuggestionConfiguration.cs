using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class PurchaseSuggestionConfiguration : IEntityTypeConfiguration<PurchaseSuggestion>
{
    public void Configure(EntityTypeBuilder<PurchaseSuggestion> builder)
    {
        builder.ToTable("purchase_suggestions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(
                new Microsoft.EntityFrameworkCore.Storage.ValueConversion
                    .ValueConverter<Domain.Common.PurchaseSuggestionId, Guid>(
                        v => v.Value,
                        v => Domain.Common.PurchaseSuggestionId.From(v)))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(ValueConverters.Tenant).IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").HasConversion(ValueConverters.Product).IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").HasConversion(ValueConverters.Branch).IsRequired();
        builder.Property(x => x.SupplierId).HasColumnName("supplier_id").HasConversion(ValueConverters.Supplier);

        builder.Property(x => x.SuggestedQuantity)
            .HasColumnName("suggested_quantity")
            .HasConversion(ValueConverters.QuantityNumber)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.ObservedBalance)
            .HasColumnName("observed_balance")
            .HasConversion(ValueConverters.QuantityNumber)
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(status => (int)status, value => (PurchaseSuggestionStatus)value)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");

        // Uma única sugestão ABERTA por (tenant, produto, filial) — dedupe do job.
        builder.HasIndex(x => new { x.TenantId, x.ProductId, x.BranchId })
            .HasDatabaseName("UX_purchase_suggestions_open")
            .IsUnique()
            .HasFilter("\"status\" = 1");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_purchase_suggestions_tenant_id");
    }
}

