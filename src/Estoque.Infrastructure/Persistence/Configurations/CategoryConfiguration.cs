using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => CategoryId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ParentCategoryId)
            .HasColumnName("parent_category_id")
            .HasConversion(Estoque.Infrastructure.Persistence.ValueConverters.Category);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.Name })
            .HasDatabaseName("UX_categories_tenant_name_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_categories_tenant_id");

        // Sem FK física entre categorias (hierarquia lógica no mesmo tenant).
        builder.HasQueryFilter("Active", c => c.IsActive);
    }
}
