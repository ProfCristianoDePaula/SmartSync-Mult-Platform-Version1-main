using Identity.Domain.Common;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.ToTable("modules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => ModuleId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        // Slug é o identificador ESTÁVEL usado pelos outros microsserviços
        // (imutável na Application; o banco garante unicidade global).
        builder.Property(x => x.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.HasIndex(x => x.Slug)
            .HasDatabaseName("IX_modules_slug")
            .IsUnique()
            .HasFilter("\"is_active\"");

        // Um módulo soft-deletado não bloqueia a reutilização do nome.
        builder.HasIndex(x => x.Name)
            .HasDatabaseName("IX_modules_name")
            .IsUnique()
            .HasFilter("\"is_active\"");

        // Named query filter (mesmo padrão de Tenant/Plan/Branch).
        builder.HasQueryFilter("Active", m => m.IsActive);
    }
}
