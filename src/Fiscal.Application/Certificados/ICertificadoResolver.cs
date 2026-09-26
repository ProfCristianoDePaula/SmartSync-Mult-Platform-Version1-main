using System.Security.Cryptography.X509Certificates;
using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Application.Certificados;

/// <summary>
/// Resolve o certificado do emitente (filial › tenant) e devolve instância
/// X509 descartável por chamada. Nunca cacheia em claro além da chamada (R4).
/// </summary>
public interface ICertificadoResolver
{
    Task<X509Certificate2> ObterAsync(
        TenantId tenantId, BranchId? branchId, AmbienteFiscal ambiente,
        CancellationToken ct = default);
}
