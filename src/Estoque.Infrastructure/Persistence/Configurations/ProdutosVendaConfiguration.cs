using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class ProdutosVendaConfiguration : IEntityTypeConfiguration<ProdutosVenda>
{
    public void Configure(EntityTypeBuilder<ProdutosVenda> builder)
    {
        builder.ToTable("produtos_venda");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.ProdutosVenda)
            .ValueGeneratedNever();

        builder.Property(x => x.IdVenda)
            .HasColumnName("id_venda")
            .HasConversion(ValueConverters.Venda)
            .IsRequired();

        builder.Property(x => x.IdProduto)
            .HasColumnName("id_produto")
            .HasConversion(ValueConverters.Product)
            .IsRequired();

        builder.Property(x => x.Quantidade)
            .HasColumnName("quantidade")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.HasIndex(x => x.IdVenda).HasDatabaseName("IX_produtos_venda_id_venda");
        builder.HasIndex(x => x.IdProduto).HasDatabaseName("IX_produtos_venda_id_produto");
        builder.HasIndex(x => new { x.IdVenda, x.IdProduto })
            .HasDatabaseName("UX_produtos_venda_venda_produto")
            .IsUnique();
    }
}
