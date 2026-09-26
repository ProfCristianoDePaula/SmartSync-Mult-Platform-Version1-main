using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;

namespace Fiscal.Application.Repositories;

// NOTE: AmbienteFiscal vem de Fiscal.Domain.Enums (Fiscal-2). Sem duplicação (R2).

public interface IMunicipioRepository
{
    Task<Municipio?> GetByIbgeAsync(string codigoIbge, CancellationToken ct = default);
    Task AddAsync(Municipio municipio, CancellationToken ct = default);
    Task<(IReadOnlyList<Municipio> Items, int Total)> ListAsync(
        string? uf, string? search, int page, int pageSize, CancellationToken ct = default);
}

public interface INfseMunicipioConfigRepository
{
    Task<NfseMunicipioConfig?> GetByIbgeAsync(string codigoIbge, CancellationToken ct = default);
    Task AddAsync(NfseMunicipioConfig config, CancellationToken ct = default);
    Task<IReadOnlyList<NfseMunicipioConfig>> ListByIbgesAsync(IEnumerable<string> ibges, CancellationToken ct = default);
}

public interface INfseAmbienteRepository
{
    Task<IReadOnlyList<NfseAmbiente>> ListByIbgeAsync(string codigoIbge, CancellationToken ct = default);
    Task<NfseAmbiente?> FindAsync(string codigoIbge, ModoEmissaoNfse modo, AmbienteFiscal ambiente, CancellationToken ct = default);
    Task AddAsync(NfseAmbiente ambiente, CancellationToken ct = default);
}
