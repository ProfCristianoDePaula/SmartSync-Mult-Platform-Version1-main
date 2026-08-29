using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => SupplierId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        // Documento achatado em 2 colunas (tipo + número) — permite o índice
        // único COMPOSTO por tenant (o padrão OwnsOne não suporta índice
        // composto com coluna do dono).
        builder.Property(x => x.TipoPessoa)
            .HasColumnName("tipo_pessoa")
            .HasConversion(tipo => (int)tipo, value => (TipoPessoa)value)
            .IsRequired();

        builder.Property(x => x.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(14)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasConversion(email => email.Value, value => Email.FromValidated(value))
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

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

        // Unicidade POR TENANT do número do documento, entre ativos.
        builder.HasIndex(x => new { x.TenantId, x.DocumentNumber })
            .HasDatabaseName("UX_suppliers_tenant_document_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_suppliers_tenant_id");

        builder.HasQueryFilter("Active", s => s.IsActive);
    }
}
