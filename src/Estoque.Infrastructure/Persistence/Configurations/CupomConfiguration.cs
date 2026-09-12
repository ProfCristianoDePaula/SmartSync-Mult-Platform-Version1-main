using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class CupomConfiguration : IEntityTypeConfiguration<Cupom>
{
    public void Configure(EntityTypeBuilder<Cupom> builder)
    {
        builder.ToTable("cupons");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.Cupom)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ValorDesconto)
            .HasColumnName("valor_desconto")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.PercDesconto)
            .HasColumnName("perc_desconto")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(x => x.ValorMinimoCompra)
            .HasColumnName("valor_minimo_compra")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.IdCategoria)
            .HasColumnName("id_categoria")
            .HasConversion(ValueConverters.Category);

        builder.Property(x => x.DataValidade)
            .HasColumnName("data_validade")
            .IsRequired();

        builder.Property(x => x.DataCriacao)
            .HasColumnName("data_criacao")
            .IsRequired();

        builder.Property(x => x.Quantidade)
            .HasColumnName("quantidade")
            .IsRequired();

        builder.Property(x => x.IsCupomProduto)
            .HasColumnName("is_cupom_produto")
            .IsRequired();

        // Multi-tenancy: todo cupom pertence a um tenant, queries sempre filtradas por tenant_id.
        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_cupons_tenant_id");
        builder.HasIndex(x => x.IdCategoria).HasDatabaseName("IX_cupons_id_categoria");
        builder.HasIndex(x => x.DataValidade).HasDatabaseName("IX_cupons_data_validade");
        builder.HasIndex(x => new { x.TenantId, x.DataValidade }).HasDatabaseName("IX_cupons_tenant_validade");
    }
}
