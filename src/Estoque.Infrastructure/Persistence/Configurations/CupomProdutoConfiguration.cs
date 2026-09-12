using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class CupomProdutoConfiguration : IEntityTypeConfiguration<CupomProduto>
{
    public void Configure(EntityTypeBuilder<CupomProduto> builder)
    {
        builder.ToTable("cupom_produtos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.CupomProduto)
            .ValueGeneratedNever();

        builder.Property(x => x.IdCupom)
            .HasColumnName("id_cupom")
            .HasConversion(ValueConverters.Cupom)
            .IsRequired();

        builder.Property(x => x.IdProduto)
            .HasColumnName("id_produto")
            .HasConversion(ValueConverters.Product)
            .IsRequired();

        // Índices relevantes (prioridade 1 na aplicação do cupom).
        builder.HasIndex(x => x.IdCupom).HasDatabaseName("IX_cupom_produtos_id_cupom");
        builder.HasIndex(x => x.IdProduto).HasDatabaseName("IX_cupom_produtos_id_produto");
        builder.HasIndex(x => new { x.IdCupom, x.IdProduto })
            .HasDatabaseName("UX_cupom_produtos_cupom_produto")
            .IsUnique();
    }
}
