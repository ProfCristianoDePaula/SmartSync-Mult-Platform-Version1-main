using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class ClienteFiscalConfiguration : IEntityTypeConfiguration<ClienteFiscal>
{
    public void Configure(EntityTypeBuilder<ClienteFiscal> builder)
    {
        builder.ToTable("clientes_fiscais");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasConversion(ValueConverters.ClienteFiscal).ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(ValueConverters.Tenant).IsRequired();
        builder.Property(x => x.ClienteId).HasColumnName("cliente_id").IsRequired();
        builder.Property(x => x.TipoPessoa).HasColumnName("tipo_pessoa").HasConversion(t => (int)t, v => (TipoPessoaFiscal)v).IsRequired();
        builder.Property(x => x.Documento).HasColumnName("documento").HasMaxLength(14).IsRequired();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(255).IsRequired();
        builder.Property(x => x.IndicadorIe).HasColumnName("ind_ie").HasConversion(i => (int)i, v => (IndicadorIe)v).IsRequired();
        builder.Property(x => x.InscricaoEstadual).HasColumnName("inscricao_estadual").HasMaxLength(20);

        builder.OwnsOne(
            x => x.Endereco,
            end =>
            {
                end.Property(e => e.Street).HasColumnName("end_logradouro").HasMaxLength(255).IsRequired();
                end.Property(e => e.Number).HasColumnName("end_numero").HasMaxLength(20).IsRequired();
                end.Property(e => e.Complement).HasColumnName("end_complemento").HasMaxLength(100);
                end.Property(e => e.District).HasColumnName("end_bairro").HasMaxLength(100).IsRequired();
                end.Property(e => e.City).HasColumnName("end_cidade").HasMaxLength(100).IsRequired();
                end.Property(e => e.State).HasColumnName("end_uf").HasMaxLength(2).IsRequired();
                end.Property(e => e.PostalCode).HasColumnName("end_cep").HasMaxLength(8).IsRequired();
                end.Property(e => e.CodigoIbgeMunicipio).HasColumnName("end_ibge").HasMaxLength(7).IsRequired();
            });

        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(x => x.ConsumidorFinal).HasColumnName("consumidor_final").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.ClienteId })
            .HasDatabaseName("UX_clientes_fiscais_tenant_cliente_active")
            .IsUnique().HasFilter("\"is_active\"");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_clientes_fiscais_tenant");

        builder.HasQueryFilter("Active", c => c.IsActive);
    }
}
