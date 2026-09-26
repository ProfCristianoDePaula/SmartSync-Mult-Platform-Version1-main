using Fiscal.Domain.Common;

namespace Fiscal.Application.Emitentes;

/// <summary>
/// Resumo de certificado para a prontidão (Fiscal-4). Implementação real
/// (cofre) chega na Fiscal-5; até lá, o Fiscal registra ausência.
/// </summary>
public sealed record CertificadoResumo(Guid Id, DateTime NotAfter, bool Expirado);

public interface ICertificadoReadModel
{
    Task<CertificadoResumo?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
}
