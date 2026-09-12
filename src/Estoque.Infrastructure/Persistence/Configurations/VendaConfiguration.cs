using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class VendaConfiguration : IEntityTypeConfiguration<Venda>
{
    public void Configure(EntityTypeBuilder<Venda> builder)
    {
        builder.ToTable("vendas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.Venda)
            .ValueGeneratedNever();

        builder.Property(x => x.IdPedido)
            .HasColumnName("id_pedido")
            .HasConversion(ValueConverters.Pedido)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.DataVenda)
            .HasColumnName("data_venda")
            .IsRequired();

        builder.Property(x => x.IdFormaPagto)
            .HasColumnName("id_forma_pagto")
            .HasConversion(ValueConverters.FormaPagto)
            .IsRequired();

        builder.Property(x => x.ValorBruto)
            .HasColumnName("valor_bruto")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.ValorDesconto)
            .HasColumnName("valor_desconto")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.ValorLiquidoPedido)
            .HasColumnName("valor_liquido_pedido")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.ValorFrete)
            .HasColumnName("valor_frete")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.ValorFinal)
            .HasColumnName("valor_final")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.IsPago)
            .HasColumnName("is_pago")
            .IsRequired();

        builder.Property(x => x.NrPedido)
            .HasColumnName("nr_pedido")
            .HasMaxLength(50);

        builder.Property(x => x.QuantidadeParcelar)
            .HasColumnName("quantidade_parcelar")
            .IsRequired();

        // Venda 1:1 com Pedido — índice único garante apenas uma venda por pedido.
        builder.HasIndex(x => x.IdPedido)
            .HasDatabaseName("UX_vendas_id_pedido")
            .IsUnique();

        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_vendas_tenant_id");
        builder.HasIndex(x => x.IdFormaPagto).HasDatabaseName("IX_vendas_id_forma_pagto");
        builder.HasIndex(x => x.DataVenda).HasDatabaseName("IX_vendas_data_venda");
        builder.HasIndex(x => new { x.TenantId, x.DataVenda }).HasDatabaseName("IX_vendas_tenant_data_venda");

        builder.Ignore(x => x.Itens);
        builder.Ignore(x => x.DomainEvents);
    }
}
