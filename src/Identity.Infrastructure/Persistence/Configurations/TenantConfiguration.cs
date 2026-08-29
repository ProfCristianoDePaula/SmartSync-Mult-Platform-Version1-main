using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => TenantId.From(value))
            .ValueGeneratedNever();

        builder.Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.TradeName)
            .HasColumnName("trade_name")
            .HasMaxLength(255)
            .IsRequired();

        // Documento do tenant (Etapa 17): CPF (pessoa física) ou CNPJ (pessoa
        // jurídica), mapeado como owned type em duas colunas. A unicidade global
        // (independente do tipo) é garantida pelo índice sobre a coluna do número.
        builder.OwnsOne(
            x => x.Documento,
            documento =>
            {
                documento.Property(d => d.Tipo)
                    .HasColumnName("tipo_pessoa")
                    .HasConversion<int>()
                    .IsRequired();

                documento.Property(d => d.Numero)
                    .HasColumnName("documento")
                    .HasMaxLength(14)
                    .IsRequired();

                // Unicidade GLOBAL do número do documento (CPF/CNPJ), com índice
                // PARCIAL (somente tenants ativos, Etapa 13): um tenant
                // soft-deletado não bloqueia a reutilização do documento, mesmo na
                // camada do banco (mesma abordagem do nome de plano na Etapa 12).
                // O índice cobre apenas a coluna "documento" — independente do tipo
                // de pessoa (Etapa 17).
                documento.HasIndex(d => d.Numero)
                    .HasDatabaseName("IX_tenants_documento")
                    .IsUnique()
                    .HasFilter("\"is_active\"");
            });

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasConversion(email => email.Value, value => Email.FromValidated(value))
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.HasMany(x => x.Branches)
            .WithOne()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unicidade GLOBAL de documento (CPF/CNPJ) e e-mail (Etapa 04) — agora
        // com índice PARCIAL (somente tenants ativos, Etapa 13): um tenant
        // soft-deletado não bloqueia a reutilização do documento/e-mail, mesmo na
        // camada do banco (mesma abordagem do nome de plano na Etapa 12). A
        // unicidade entre ativos continua garantida tanto pelo banco quanto pela
        // Application. O índice de documento vive dentro do OwnsOne (Etapa 17).
        builder.HasIndex(x => x.Email)
            .HasDatabaseName("IX_tenants_email")
            .IsUnique()
            .HasFilter("\"is_active\"");

        // Soft delete (Etapa 13) via named query filter: por padrão, tenants
        // inativados ficam fora das consultas. ListTenants(includeInactive) usa
        // IgnoreQueryFilters("Active"). O mesmo filtro faz a checagem de
        // unicidade de documento/e-mail ignorar tenants soft-deletados (Etapa 04)
        // e bloqueia o login dos usuários vinculados (Etapa 13).
        builder.HasQueryFilter("Active", t => t.IsActive);
    }
}