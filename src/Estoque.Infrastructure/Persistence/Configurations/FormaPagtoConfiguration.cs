using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Estoque.Infrastructure.Persistence.Configurations;

public sealed class FormaPagtoConfiguration : IEntityTypeConfiguration<FormaPagto>
{
    public void Configure(EntityTypeBuilder<FormaPagto> builder)
    {
        builder.ToTable("formas_pagto");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.FormaPagto)
            .ValueGeneratedNever();

        builder.Property(x => x.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.QtdMaximaParcelas)
            .HasColumnName("qtd_maxima_parcelas")
            .IsRequired();

        // Catálogo global — descrição única (Pix, Cartão...).
        builder.HasIndex(x => x.Descricao)
            .HasDatabaseName("UX_formas_pagto_descricao")
            .IsUnique();
    }
}
