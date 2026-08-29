using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => BranchId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        // Soft delete (Etapa 14) — mesma abordagem de Tenant/Plan: named query
        // filter "Active" oculta filiais inativas por padrão; a listagem pode
        // quebrá-lo com IgnoreQueryFilters(["Active"]). O default true evita que
        // filiais pré-existentes nasçam inativas no backfill.
        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.OwnsOne(
            x => x.Address,
            address =>
            {
                address.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(255).IsRequired();
                address.Property(a => a.Number).HasColumnName("address_number").HasMaxLength(20).IsRequired();
                address.Property(a => a.Complement).HasColumnName("address_complement").HasMaxLength(100);
                address.Property(a => a.District).HasColumnName("address_district").HasMaxLength(100).IsRequired();
                address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100).IsRequired();
                address.Property(a => a.State).HasColumnName("address_state").HasMaxLength(2).IsRequired();
                address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(8).IsRequired();
            });

        builder.OwnsOne(
            x => x.Contact,
            contact =>
            {
                contact.Property(c => c.Phone).HasColumnName("contact_phone").HasMaxLength(11).IsRequired();
                contact.Property(c => c.SecondaryPhone).HasColumnName("contact_secondary_phone").HasMaxLength(11);
                contact.Property(c => c.Email)
                    .HasColumnName("contact_email")
                    .HasConversion(email => email.Value, value => Email.FromValidated(value))
                    .HasMaxLength(254)
                    .IsRequired();
            });

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_branches_tenant_id");

        // Named query filter (EF Core 10): por padrão filiais soft-deletadas
        // ficam fora de todas as consultas (mesmo padrão de Tenant/Plan).
        builder.HasQueryFilter("Active", b => b.IsActive);
    }
}