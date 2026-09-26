using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracaoDocumentoConfiguration : IEntityTypeConfiguration<ConfiguracaoDocumento>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoDocumento> builder)
    {
        builder.ToTable("configuracoes_documento");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.ConfiguracaoDocumento)
            .ValueGeneratedNever();

        builder.Property(x => x.EmitenteId)
            .HasColumnName("emitente_id")
            .HasConversion(ValueConverters.EmitenteFiscal)
            .IsRequired();

        builder.Property(x => x.Tipo)
            .HasColumnName("tipo")
            .HasConversion(t => (int)t, v => (TipoDocumentoFiscal)v)
            .IsRequired();

        builder.Property(x => x.Ambiente)
            .HasColumnName("ambiente")
            .HasConversion(a => (int)a, v => (AmbienteFiscal)v)
            .IsRequired();

        builder.Property(x => x.Habilitado).HasColumnName("habilitado").IsRequired();
        builder.Property(x => x.Serie).HasColumnName("serie").HasMaxLength(3).IsRequired();

        builder.Property(x => x.ModoIntegracao)
            .HasColumnName("modo_integracao")
            .HasConversion(m => (int)m, v => (ModoIntegracao)v)
            .IsRequired();

        builder.Property(x => x.ReferenciaCertificado).HasColumnName("referencia_certificado");
        builder.Property(x => x.CscId).HasColumnName("csc_id").HasMaxLength(20);
        builder.Property(x => x.CscTokenCifrado).HasColumnName("csc_token_cifrado").HasColumnType("bytea");
        builder.Property(x => x.CscTokenKeyId).HasColumnName("csc_token_key_id").HasMaxLength(50);
        builder.Property(x => x.HomologacaoValidadaEm).HasColumnName("homologacao_validada_em");

        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");

        builder.HasIndex(x => new { x.EmitenteId, x.Tipo, x.Ambiente })
            .HasDatabaseName("UX_config_doc_emitente_tipo_amb_active")
            .IsUnique()
            .HasFilter("\"is_active\"");

        builder.HasIndex(x => x.EmitenteId).HasDatabaseName("IX_config_doc_emitente");

        builder.HasQueryFilter("Active", c => c.IsActive);
    }
}
