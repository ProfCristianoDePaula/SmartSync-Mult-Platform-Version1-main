using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class ProdutoFiscalConfiguration : IEntityTypeConfiguration<ProdutoFiscal>
{
    public void Configure(EntityTypeBuilder<ProdutoFiscal> builder)
    {
        builder.ToTable("produtos_fiscais");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasConversion(ValueConverters.ProdutoFiscal).ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(ValueConverters.Tenant).IsRequired();
        builder.Property(x => x.ProdutoId).HasColumnName("produto_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion(t => (int)t, v => (TipoItemFiscal)v).IsRequired();

        builder.Property(x => x.Ncm).HasColumnName("ncm").HasMaxLength(8);
        builder.Property(x => x.Cest).HasColumnName("cest").HasMaxLength(7);
        builder.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(1);
        builder.Property(x => x.UnidadeComercial).HasColumnName("un_comercial").HasMaxLength(6);
        builder.Property(x => x.UnidadeTributavel).HasColumnName("un_tributavel").HasMaxLength(6);
        builder.Property(x => x.FatorConversao).HasColumnName("fator_conversao").HasColumnType("numeric(18,6)");
        builder.Property(x => x.Gtin).HasColumnName("gtin").HasMaxLength(14);
        builder.Property(x => x.CfopDentroUf).HasColumnName("cfop_dentro_uf").HasMaxLength(4);
        builder.Property(x => x.CfopForaUf).HasColumnName("cfop_fora_uf").HasMaxLength(4);
        builder.Property(x => x.CstIcms).HasColumnName("cst_icms").HasMaxLength(3);
        builder.Property(x => x.Csosn).HasColumnName("csosn").HasMaxLength(3);
        builder.Property(x => x.AliquotaIcms).HasColumnName("aliq_icms").HasColumnType("numeric(7,4)");
        builder.Property(x => x.CstPis).HasColumnName("cst_pis").HasMaxLength(2);
        builder.Property(x => x.CstCofins).HasColumnName("cst_cofins").HasMaxLength(2);
        builder.Property(x => x.AliquotaPis).HasColumnName("aliq_pis").HasColumnType("numeric(7,4)");
        builder.Property(x => x.AliquotaCofins).HasColumnName("aliq_cofins").HasColumnType("numeric(7,4)");
        builder.Property(x => x.CstIpi).HasColumnName("cst_ipi").HasMaxLength(2);
        builder.Property(x => x.AliquotaIpi).HasColumnName("aliq_ipi").HasColumnType("numeric(7,4)");
        builder.Property(x => x.CClassTrib).HasColumnName("cclasstrib").HasMaxLength(6);
        builder.Property(x => x.CstIbsCbs).HasColumnName("cst_ibscbs").HasMaxLength(3);
        builder.Property(x => x.ItemLc116).HasColumnName("item_lc116").HasMaxLength(10);
        builder.Property(x => x.Nbs).HasColumnName("nbs").HasMaxLength(10);
        builder.Property(x => x.CodigoTribNacional).HasColumnName("cod_trib_nacional").HasMaxLength(10);
        builder.Property(x => x.AliquotaIss).HasColumnName("aliq_iss").HasColumnType("numeric(7,4)");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.ProdutoId })
            .HasDatabaseName("UX_produtos_fiscais_tenant_produto_active")
            .IsUnique().HasFilter("\"is_active\"");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_produtos_fiscais_tenant");

        builder.HasQueryFilter("Active", p => p.IsActive);
    }
}
