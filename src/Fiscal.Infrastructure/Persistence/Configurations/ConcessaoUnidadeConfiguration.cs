using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class ConcessaoUnidadeConfiguration : IEntityTypeConfiguration<ConcessaoUnidade>
{
    public void Configure(EntityTypeBuilder<ConcessaoUnidade> builder)
    {
        builder.ToTable("concessoes_unidade");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.ConcessaoUnidade)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasConversion(ValueConverters.Branch)
            .IsRequired();

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(x => x.Papel)
            .HasColumnName("papel")
            .HasConversion(p => (int)p, v => (PapelUnidade)v)
            .IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.UserId, x.Papel })
            .HasDatabaseName("UX_concessoes_tenant_branch_user_papel_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("IX_concessoes_tenant_user");

        builder.HasQueryFilter("Active", c => c.IsActive);
    }
}
