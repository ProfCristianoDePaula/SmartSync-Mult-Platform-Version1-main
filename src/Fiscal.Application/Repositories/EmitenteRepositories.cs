using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Application.Repositories;

public interface IEmitenteFiscalRepository
{
    Task<EmitenteFiscal?> GetAsync(TenantId tenantId, Guid emitenteId, CancellationToken ct = default);
    Task<EmitenteFiscal?> GetByBranchAsync(TenantId tenantId, BranchId branchId, CancellationToken ct = default);
    Task AddAsync(EmitenteFiscal emitente, CancellationToken ct = default);
    Task<IReadOnlyList<EmitenteFiscal>> ListAsync(TenantId tenantId, CancellationToken ct = default);
}

public interface IConfiguracaoDocumentoRepository
{
    Task<ConfiguracaoDocumento?> FindAsync(EmitenteFiscalId emitenteId, TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, CancellationToken ct = default);
    Task<IReadOnlyList<ConfiguracaoDocumento>> ListByEmitenteAsync(EmitenteFiscalId emitenteId, CancellationToken ct = default);
    Task AddAsync(ConfiguracaoDocumento config, CancellationToken ct = default);
}

public interface IConcessaoRepository
{
    Task<ConcessaoUnidade?> GetAsync(TenantId tenantId, Guid concessaoId, CancellationToken ct = default);
    Task<ConcessaoUnidade?> FindAsync(TenantId tenantId, BranchId branchId, Guid userId, PapelUnidade papel, CancellationToken ct = default);
    Task AddAsync(ConcessaoUnidade concessao, CancellationToken ct = default);
    Task<IReadOnlyList<ConcessaoUnidade>> ListAsync(TenantId tenantId, BranchId? branchId, CancellationToken ct = default);
}
