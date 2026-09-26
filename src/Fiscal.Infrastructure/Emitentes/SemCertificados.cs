using Fiscal.Application.Emitentes;
using Fiscal.Domain.Common;

namespace Fiscal.Infrastructure.Emitentes;

/// <summary>
/// Placeholder até a Fiscal-5 (cofre de certificados): nenhum certificado
/// existe, então a prontidão sempre marca "certificado" como pendente.
/// Substituído pela implementação real sem mudar a interface.
/// </summary>
public sealed class SemCertificados : ICertificadoReadModel
{
    public Task<CertificadoResumo?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult<CertificadoResumo?>(null);
}
