namespace Fiscal.Domain.Enums;

/// <summary>Status do certificado (A3/nuvem indisponíveis até implementação específica).</summary>
public enum CertificadoStatus
{
    Ativo = 1,
    Expirando = 2,
    Expirado = 3,
    Revogado = 4
}
