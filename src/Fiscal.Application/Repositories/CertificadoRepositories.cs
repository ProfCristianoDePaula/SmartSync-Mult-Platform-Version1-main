using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;

namespace Fiscal.Application.Repositories;

public interface ICertificadoRepository
{
    Task<CertificadoDigital?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CertificadoDigital>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<CertificadoDigital>> ListAtivosAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default);
    Task AddAsync(CertificadoDigital certificado, CancellationToken ct = default);
}

public interface IAlertaRepository
{
    Task<AlertaFiscal?> GetAsync(TenantId? tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ExisteNaoLidoAsync(TenantId? tenantId, string tipo, string? entityId, CancellationToken ct = default);
    Task AddAsync(AlertaFiscal alerta, CancellationToken ct = default);
    Task<IReadOnlyList<AlertaFiscal>> ListAsync(TenantId? tenantId, bool apenasNaoLidas, CancellationToken ct = default);
}
