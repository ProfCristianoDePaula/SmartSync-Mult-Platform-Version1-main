using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class XmlImportConfiguration : IEntityTypeConfiguration<XmlImport>
{
    public void Configure(EntityTypeBuilder<XmlImport> builder)
    {
        builder.ToTable("xml_imports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(
                new ValueConverter<Domain.Common.XmlImportId, Guid>(
                    v => v.Value, v => Domain.Common.XmlImportId.From(v)))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasConversion(ValueConverters.Branch)
            .IsRequired();

        builder.Property(x => x.SupplierId)
            .HasColumnName("supplier_id")
            .HasConversion(ValueConverters.Supplier);

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        // Conteúdo do XML (limite 5 MB validado no domínio).
        builder.Property(x => x.Content).HasColumnName("content");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(status => (int)status, value => (XmlImportStatus)value)
            .IsRequired();

        builder.Property(x => x.TotalItems).HasColumnName("total_items");
        builder.Property(x => x.ProcessedItems).HasColumnName("processed_items");
        builder.Property(x => x.SkippedItems).HasColumnName("skipped_items");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");

        // Fila de processamento do worker.
        builder.HasIndex(x => new { x.Status })
            .HasDatabaseName("IX_xml_imports_status");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_xml_imports_tenant_id");
    }
}

