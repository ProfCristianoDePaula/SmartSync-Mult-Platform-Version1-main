using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Application.Repositories;

public interface IUfFiscalRepository
{
    Task<UfFiscal?> GetBySiglaAsync(string sigla, CancellationToken ct = default);
    Task<UfFiscal?> GetByCodigoIbgeAsync(int codigoIbge, CancellationToken ct = default);
    Task AddAsync(UfFiscal uf, CancellationToken ct = default);
    Task<IReadOnlyList<UfFiscal>> ListAsync(CancellationToken ct = default);
}

public interface ISefazEndpointRepository
{
    Task<SefazEndpoint?> GetAsync(Guid id, CancellationToken ct = default);
    Task<SefazEndpoint?> FindActiveAsync(
        string autorizador, ModeloFiscal modelo, SefazServico servico,
        AmbienteFiscal ambiente, string versaoServico, CancellationToken ct = default);
    Task<IReadOnlyList<SefazEndpoint>> ListByAutorizadorAsync(string autorizador, CancellationToken ct = default);
    Task<IReadOnlyList<SefazEndpoint>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(SefazEndpoint endpoint, CancellationToken ct = default);
}
