using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

/// <summary>
/// Movimentação — APPEND-ONLY (log de auditoria). Nenhum UPDATE é executado
/// por código de aplicação; sem soft delete.
/// </summary>
public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.StockMovement)
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

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion(type => (int)type, value => (MovementType)value)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasConversion(q => q.Value, value => Quantity.Positive(value))
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.Property(x => x.UnitCost)
            .HasColumnName("unit_cost")
            .HasConversion(ValueConverters.MoneyNumber)
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.LotId).HasColumnName("lot_id").HasConversion(ValueConverters.Lot);

        builder.Property(x => x.OriginDocumentRef)
            .HasColumnName("origin_document_ref")
            .HasMaxLength(200);

        builder.Property(x => x.PerformedByUserId).HasColumnName("performed_by_user_id");
        builder.Property(x => x.CounterpartyBranchId).HasColumnName("counterparty_branch_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        // Consultas típicas: histórico do produto na filial; feed por tenant.
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductId })
            .HasDatabaseName("IX_stock_movements_tenant_branch_product");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAtUtc })
            .HasDatabaseName("IX_stock_movements_tenant_created_at");
    }
}

