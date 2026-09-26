using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fiscal.Infrastructure.Persistence.Configurations;

public sealed class CertificadoDigitalConfiguration : IEntityTypeConfiguration<CertificadoDigital>
{
    public void Configure(EntityTypeBuilder<CertificadoDigital> builder)
    {
        builder.ToTable("certificados_digitais");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(ValueConverters.CertificadoDigital)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(ValueConverters.Tenant)
            .IsRequired();

        builder.Property(x => x.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CnpjDoCertificado).HasColumnName("cnpj_certificado").HasMaxLength(14).IsRequired();
        builder.Property(x => x.Thumbprint).HasColumnName("thumbprint").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NotBefore).HasColumnName("not_before").IsRequired();
        builder.Property(x => x.NotAfter).HasColumnName("not_after").IsRequired();

        // Segredos cifrados (R4): nunca em claro, nunca indexados por conteúdo.
        builder.Property(x => x.PfxCifrado).HasColumnName("pfx_cifrado").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.SenhaCifrada).HasColumnName("senha_cifrada").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.KeyId).HasColumnName("key_id").HasMaxLength(50).IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(s => (int)s, v => (CertificadoStatus)v)
            .IsRequired();

        builder.Property(x => x.EnviadoPor).HasColumnName("enviado_por").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");

        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.Status })
            .HasDatabaseName("IX_certificados_tenant_branch_status");

        builder.HasIndex(x => x.NotAfter).HasDatabaseName("IX_certificados_not_after");
    }
}
