using Identity.Domain.Common;
using Identity.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.Property(u => u.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                new ValueConverter<TenantId?, Guid?>(
                    v => v.HasValue ? v.Value.Value : null,
                    v => v.HasValue ? TenantId.From(v.Value) : null));

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(255);

        builder.Property(u => u.Document)
            .HasColumnName("document")
            .HasMaxLength(18);

        builder.Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()");

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .HasDatabaseName("IX_users_tenant_email")
            .IsUnique();

        builder.HasIndex(u => new { u.TenantId, u.Document })
            .HasDatabaseName("IX_users_tenant_document")
            .IsUnique();
    }
}