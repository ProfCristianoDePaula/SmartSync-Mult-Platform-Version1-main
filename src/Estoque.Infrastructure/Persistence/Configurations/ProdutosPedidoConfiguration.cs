using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class ProdutosPedidoConfiguration : IEntityTypeConfiguration<ProdutosPedido>
{
    public void Configure(EntityTypeBuilder<ProdutosPedido> builder)
    {
        builder.ToTable("produtos_pedido");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.ProdutosPedido)
            .ValueGeneratedNever();

        // Nome canônico IdPedido; coluna mantém spec IdCarrinho compatível como "id_pedido"
        builder.Property(x => x.IdPedido)
            .HasColumnName("id_pedido")
            .HasConversion(ValueConverters.Pedido)
            .IsRequired();

        builder.Property(x => x.IdProduto)
            .HasColumnName("id_produto")
            .HasConversion(ValueConverters.Product)
            .IsRequired();

        builder.Property(x => x.Quantidade)
            .HasColumnName("quantidade")
            .HasColumnType("numeric(18,4)")
            .IsRequired();

        builder.HasIndex(x => x.IdPedido).HasDatabaseName("IX_produtos_pedido_id_pedido");
        builder.HasIndex(x => x.IdProduto).HasDatabaseName("IX_produtos_pedido_id_produto");
        builder.HasIndex(x => new { x.IdPedido, x.IdProduto })
            .HasDatabaseName("UX_produtos_pedido_pedido_produto")
            .IsUnique();
    }
}
