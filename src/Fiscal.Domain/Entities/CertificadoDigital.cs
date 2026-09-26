using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Certificado A1: metadados em claro + PFX/senha CIFRADOS (nunca em texto
/// claro, nunca devolvidos). `BranchId` vazio = escopo do tenant (padrão por
/// CNPJ-base); com valor = override da filial. Upload novo no mesmo escopo
/// revoga o anterior (histórico mantido).
/// </summary>
public sealed class CertificadoDigital : Entity<CertificadoDigitalId>
{
    public TenantId TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Tipo { get; private set; } = "A1";
    public string CnpjDoCertificado { get; private set; } = null!;
    public string Thumbprint { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public DateTime NotBefore { get; private set; }
    public DateTime NotAfter { get; private set; }
    public byte[] PfxCifrado { get; private set; } = [];
    public byte[] SenhaCifrada { get; private set; } = [];
    public string KeyId { get; private set; } = null!;
    public CertificadoStatus Status { get; private set; }
    public Guid EnviadoPor { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CertificadoDigital() { }

    private CertificadoDigital(
        CertificadoDigitalId id, TenantId tenantId, Guid branchId, string cnpj,
        string thumbprint, string subject, DateTime notBefore, DateTime notAfter,
        byte[] pfxCifrado, byte[] senhaCifrada, string keyId, Guid enviadoPor)
        : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        CnpjDoCertificado = cnpj;
        Thumbprint = thumbprint;
        Subject = subject;
        NotBefore = notBefore;
        NotAfter = notAfter;
        PfxCifrado = pfxCifrado;
        SenhaCifrada = senhaCifrada;
        KeyId = keyId;
        Status = CertificadoStatus.Ativo;
        EnviadoPor = enviadoPor;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CertificadoDigital Create(
        TenantId tenantId, Guid branchId, string cnpj,
        string thumbprint, string subject, DateTime notBefore, DateTime notAfter,
        byte[] pfxCifrado, byte[] senhaCifrada, string keyId, Guid enviadoPor)
        => new(CertificadoDigitalId.New(), tenantId, branchId, cnpj, thumbprint,
            subject, notBefore, notAfter, pfxCifrado, senhaCifrada, keyId, enviadoPor);

    public bool EscopoTenant => BranchId == Guid.Empty;

    public void Revogar()
    {
        if (Status == CertificadoStatus.Revogado) return;
        Status = CertificadoStatus.Revogado;
    }

    public void MarcarStatus(CertificadoStatus status)
    {
        if (Status == CertificadoStatus.Revogado) return;
        Status = status;
    }

    /// <summary>Troca o envelope para a chave ativa (rotação oportunista).</summary>
    public void Reenvelopar(byte[] pfxCifrado, byte[] senhaCifrada, string keyId)
    {
        PfxCifrado = pfxCifrado;
        SenhaCifrada = senhaCifrada;
        KeyId = keyId;
    }
}
