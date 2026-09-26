using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;

namespace Fiscal.Application.Repositories;

public interface IProdutoFiscalRepository
{
    Task<ProdutoFiscal?> GetAsync(TenantId tenantId, Guid produtoId, CancellationToken ct = default);
    Task AddAsync(ProdutoFiscal produto, CancellationToken ct = default);
}

public interface IClienteFiscalRepository
{
    Task<ClienteFiscal?> GetAsync(TenantId tenantId, Guid clienteId, CancellationToken ct = default);
    Task AddAsync(ClienteFiscal cliente, CancellationToken ct = default);
}

public interface INaturezaRepository
{
    Task<NaturezaOperacao?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<NaturezaOperacao?> GetByCodigoAsync(TenantId tenantId, string codigo, CancellationToken ct = default);
    Task AddAsync(NaturezaOperacao natureza, CancellationToken ct = default);
    Task<IReadOnlyList<NaturezaOperacao>> ListAsync(TenantId tenantId, CancellationToken ct = default);
}
