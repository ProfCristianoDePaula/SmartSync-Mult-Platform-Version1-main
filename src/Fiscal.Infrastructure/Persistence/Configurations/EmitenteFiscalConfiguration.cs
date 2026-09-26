using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class EmitenteFiscalConfiguration : IEntityTypeConfiguration<EmitenteFiscal>
{
    public void Configure(EntityTypeBuilder<EmitenteFiscal> builder)
    {
        builder.ToTable("emitentes_fiscais");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.EmitenteFiscal)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasConversion(ValueConverters.Branch)
            .IsRequired();

        builder.OwnsOne(
            x => x.Cnpj,
            cnpj =>
            {
                cnpj.Property(c => c.Numero).HasColumnName("cnpj").HasMaxLength(14).IsRequired();
                cnpj.Property(c => c.Alfanumerico).HasColumnName("cnpj_alfanumerico").IsRequired();
            });

        builder.Property(x => x.RazaoSocial).HasColumnName("razao_social").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Fantasia).HasColumnName("fantasia").HasMaxLength(255).IsRequired();
        builder.Property(x => x.InscricaoEstadual).HasColumnName("inscricao_estadual").HasMaxLength(20);
        builder.Property(x => x.InscricaoMunicipal).HasColumnName("inscricao_municipal").HasMaxLength(20);
        builder.Property(x => x.Cnae).HasColumnName("cnae").HasMaxLength(7);

        builder.Property(x => x.Crt)
            .HasColumnName("crt")
            .HasConversion(c => (int)c, v => (CrtFiscal)v)
            .IsRequired();

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

        builder.Property(x => x.Telefone).HasColumnName("telefone").HasMaxLength(11).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.EmissaoAutomaticaVenda).HasColumnName("emissao_automatica_venda").HasDefaultValue(false).IsRequired();

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        // Um emitente por (tenant, filial) entre ativos.
        builder.HasIndex(x => new { x.TenantId, x.BranchId })
            .HasDatabaseName("UX_emitentes_tenant_branch_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_emitentes_tenant_id");

        builder.HasQueryFilter("Active", e => e.IsActive);
    }
}
