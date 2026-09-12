using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.Pedido)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.DataAbertura)
            .HasColumnName("data_abertura")
            .IsRequired();

        builder.Property(x => x.IdCliente)
            .HasColumnName("id_cliente");

        builder.Property(x => x.IdCupom)
            .HasColumnName("id_cupom")
            .HasConversion(ValueConverters.Cupom);

        // IdUnidade do spec = BranchId (filial/unidade de origem do carrinho)
        builder.Property(x => x.IdUnidade)
            .HasColumnName("id_unidade")
            .HasConversion(ValueConverters.Branch);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(s => (int)s, v => (PedidoStatus)v)
            .IsRequired();

        builder.Property(x => x.DataFechamento)
            .HasColumnName("data_fechamento");

        builder.Property(x => x.ValorTotal)
            .HasColumnName("valor_total")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        // Índices multi-tenant (consultas por cliente/unidade/status).
        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_pedidos_tenant_id");
        builder.HasIndex(x => x.IdCliente).HasDatabaseName("IX_pedidos_id_cliente");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_pedidos_status");
        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("IX_pedidos_tenant_status");
        builder.HasIndex(x => new { x.TenantId, x.IdCliente }).HasDatabaseName("IX_pedidos_tenant_cliente");
        builder.HasIndex(x => x.IdCupom).HasDatabaseName("IX_pedidos_id_cupom");
        builder.HasIndex(x => x.IdUnidade).HasDatabaseName("IX_pedidos_id_unidade");

        // Filtro de multi-tenancy é manual (Where TenantId) — sem HasQueryFilter global,
        // idêntico a Product/Stock. IdCliente + Status são os filtros mais usados.
        builder.Ignore(x => x.Itens);
        builder.Ignore(x => x.DomainEvents);
    }
}
