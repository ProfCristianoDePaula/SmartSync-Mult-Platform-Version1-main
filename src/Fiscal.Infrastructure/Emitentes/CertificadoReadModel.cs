using Fiscal.Application.Emitentes;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Infrastructure.Emitentes;

/// <summary>
/// Implementação real do read-model de certificados (Fiscal-5): substitui
/// `SemCertificados` sem mudar a interface. Válido = Ativo/Expirando vigente.
/// </summary>
public sealed class CertificadoReadModel(ICertificadoRepository certificados) : ICertificadoReadModel
{
    public async Task<CertificadoResumo?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
    {
        var cert = await certificados.GetAsync(tenantId, id, ct);
        if (cert is null) return null;
        var agora = DateTime.UtcNow;
        var expirado = cert.Status == CertificadoStatus.Expirado || cert.NotAfter <= agora;
        return new CertificadoResumo(cert.Id.Value, cert.NotAfter, expirado);
    }
}
