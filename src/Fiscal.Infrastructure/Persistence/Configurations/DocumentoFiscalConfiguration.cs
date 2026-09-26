using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class DocumentoFiscalConfiguration : IEntityTypeConfiguration<DocumentoFiscal>
{
    public void Configure(EntityTypeBuilder<DocumentoFiscal> builder)
    {
        builder.ToTable("documentos_fiscais");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasConversion(ValueConverters.DocumentoFiscal).ValueGeneratedNever();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasConversion(ValueConverters.Tenant).IsRequired();
        builder.Property(x => x.EmitenteId).HasColumnName("emitente_id").HasConversion(ValueConverters.EmitenteFiscal).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasConversion(t => (int)t, v => (TipoDocumentoFiscal)v).IsRequired();
        builder.Property(x => x.Ambiente).HasColumnName("ambiente").HasConversion(a => (int)a, v => (AmbienteFiscal)v).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(s => (int)s, v => (StatusDocumentoFiscal)v).IsRequired();
        builder.Property(x => x.Serie).HasColumnName("serie").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Numero).HasColumnName("numero").IsRequired();
        builder.Property(x => x.ChaveAcesso).HasColumnName("chave_acesso").HasMaxLength(44);
        builder.Property(x => x.Protocolo).HasColumnName("protocolo").HasMaxLength(50);
        builder.Property(x => x.CStat).HasColumnName("cstat").HasMaxLength(10);
        builder.Property(x => x.Motivo).HasColumnName("motivo").HasMaxLength(1000);
        builder.Property(x => x.OrigemVendaId).HasColumnName("origem_venda_id");
        builder.Property(x => x.OrigemPedidoId).HasColumnName("origem_pedido_id");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.SnapshotEmitente).HasColumnName("snapshot_emitente").HasColumnType("text").IsRequired();
        builder.Property(x => x.SnapshotDestinatario).HasColumnName("snapshot_destinatario").HasColumnType("text").IsRequired();
        builder.Property(x => x.SnapshotItens).HasColumnName("snapshot_itens").HasColumnType("text").IsRequired();
        builder.Property(x => x.SnapshotTotais).HasColumnName("snapshot_totais").HasColumnType("text").IsRequired();
        builder.Property(x => x.Tentativas).HasColumnName("tentativas").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.ProximaTentativaEm).HasColumnName("proxima_tentativa_em");
        builder.Property(x => x.XmlUltimoEnvio).HasColumnName("xml_ultimo_envio").HasColumnType("text");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");

        // Idempotência no banco (R7): sem DELETE de documentos (R9), unicidade absoluta.
        builder.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .HasDatabaseName("UX_documentos_tenant_idempotency")
            .IsUnique();

        builder.HasIndex(x => x.ChaveAcesso)
            .HasDatabaseName("UX_documentos_chave")
            .IsUnique()
            .HasFilter("\"chave_acesso\" IS NOT NULL");

        builder.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("IX_documentos_tenant_status");
        builder.HasIndex(x => x.EmitenteId).HasDatabaseName("IX_documentos_emitente");
    }
}
