using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence.Repositories;

public sealed class UfFiscalRepository(FiscalDbContext db) : IUfFiscalRepository
{
    public Task<UfFiscal?> GetBySiglaAsync(string sigla, CancellationToken ct = default)
        => db.UfsFiscais.FirstOrDefaultAsync(u => u.Sigla == sigla.Trim().ToUpperInvariant(), ct);

    public Task<UfFiscal?> GetByCodigoIbgeAsync(int codigoIbge, CancellationToken ct = default)
        => db.UfsFiscais.FirstOrDefaultAsync(u => u.CodigoIbge == codigoIbge, ct);

    public async Task AddAsync(UfFiscal uf, CancellationToken ct = default)
        => await db.UfsFiscais.AddAsync(uf, ct);

    public async Task<IReadOnlyList<UfFiscal>> ListAsync(CancellationToken ct = default)
        => await db.UfsFiscais.AsNoTracking().OrderBy(u => u.Sigla).ToListAsync(ct);
}

public sealed class SefazEndpointRepository(FiscalDbContext db) : ISefazEndpointRepository
{
    public Task<SefazEndpoint?> GetAsync(Guid id, CancellationToken ct = default)
        => db.SefazEndpoints.FirstOrDefaultAsync(e => e.Id == SefazEndpointId.From(id), ct);

    public Task<SefazEndpoint?> FindActiveAsync(
        string autorizador, ModeloFiscal modelo, SefazServico servico,
        AmbienteFiscal ambiente, string versaoServico, CancellationToken ct = default)
        => db.SefazEndpoints.FirstOrDefaultAsync(e =>
            e.Autorizador == autorizador.Trim().ToUpperInvariant() &&
            e.Modelo == modelo && e.Servico == servico &&
            e.Ambiente == ambiente && e.VersaoServico == versaoServico.Trim(), ct);

    public async Task<IReadOnlyList<SefazEndpoint>> ListByAutorizadorAsync(string autorizador, CancellationToken ct = default)
        => await db.SefazEndpoints.AsNoTracking()
            .Where(e => e.Autorizador == autorizador.Trim().ToUpperInvariant())
            .OrderBy(e => e.Modelo).ThenBy(e => e.Ambiente).ThenBy(e => e.Servico)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SefazEndpoint>> ListAllAsync(CancellationToken ct = default)
        => await db.SefazEndpoints.AsNoTracking()
            .OrderBy(e => e.Autorizador).ThenBy(e => e.Modelo).ThenBy(e => e.Ambiente).ThenBy(e => e.Servico)
            .ToListAsync(ct);

    public async Task AddAsync(SefazEndpoint endpoint, CancellationToken ct = default)
        => await db.SefazEndpoints.AddAsync(endpoint, ct);
}
