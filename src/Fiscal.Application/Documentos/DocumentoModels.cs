using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Application.Documentos;

public interface IDocumentoFiscalRepository
{
    Task<Fiscal.Domain.Entities.DocumentoFiscal?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<Fiscal.Domain.Entities.DocumentoFiscal?> GetByIdempotencyAsync(TenantId tenantId, string key, CancellationToken ct = default);
    Task<IReadOnlyList<Fiscal.Domain.Entities.DocumentoFiscal>> ListByVendaAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default);
    Task AddAsync(Fiscal.Domain.Entities.DocumentoFiscal documento, CancellationToken ct = default);
}

public interface ISerieNumeracaoService
{
    /// <summary>Reserva o próximo número atomicamente (UPDATE ... RETURNING em transação).</summary>
    Task<int> ReservarAsync(EmitenteFiscalId emitenteId, int modelo, string serie, AmbienteFiscal ambiente, CancellationToken ct = default);
}
