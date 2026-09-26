using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class SefazEndpointConfiguration : IEntityTypeConfiguration<SefazEndpoint>
{
    public void Configure(EntityTypeBuilder<SefazEndpoint> builder)
    {
        builder.ToTable("sefaz_endpoints");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.SefazEndpoint)
            .ValueGeneratedNever();

        builder.Property(x => x.Autorizador)
            .HasColumnName("autorizador")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Modelo)
            .HasColumnName("modelo")
            .HasConversion(m => (int)m, v => (ModeloFiscal)v)
            .IsRequired();

        builder.Property(x => x.Servico)
            .HasColumnName("servico")
            .HasConversion(s => (int)s, v => (SefazServico)v)
            .IsRequired();

        builder.Property(x => x.Ambiente)
            .HasColumnName("ambiente")
            .HasConversion(a => (int)a, v => (AmbienteFiscal)v)
            .IsRequired();

        builder.Property(x => x.VersaoServico)
            .HasColumnName("versao_servico")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasColumnName("url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.VigenciaInicio).HasColumnName("vigencia_inicio");
        builder.Property(x => x.VigenciaFim).HasColumnName("vigencia_fim");

        builder.Property(x => x.FonteUrl)
            .HasColumnName("fonte_url")
            .HasMaxLength(500);

        builder.Property(x => x.VerificadoEm).HasColumnName("verificado_em");

        builder.Property(x => x.Verificado)
            .HasColumnName("verificado")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        // Chave natural única entre ativos: um endpoint vigente por
        // (autorizador, modelo, serviço, ambiente, versão).
        builder.HasIndex(x => new { x.Autorizador, x.Modelo, x.Servico, x.Ambiente, x.VersaoServico })
            .HasDatabaseName("UX_sefaz_endpoints_natural_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.Autorizador).HasDatabaseName("IX_sefaz_endpoints_autorizador");
        builder.HasIndex(x => new { x.Autorizador, x.Ambiente }).HasDatabaseName("IX_sefaz_endpoints_autorizador_ambiente");

        builder.HasQueryFilter("Active", e => e.IsActive);
    }
}
