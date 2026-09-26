using Fiscal.Application.Repositories;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence.Repositories;

public sealed class MunicipioRepository(FiscalDbContext db) : IMunicipioRepository
{
    public Task<Municipio?> GetByIbgeAsync(string codigoIbge, CancellationToken ct = default)
        => db.Municipios.FirstOrDefaultAsync(m => m.CodigoIbge == Norm(codigoIbge), ct);

    public async Task AddAsync(Municipio municipio, CancellationToken ct = default)
        => await db.Municipios.AddAsync(municipio, ct);

    public async Task<(IReadOnlyList<Municipio> Items, int Total)> ListAsync(
        string? uf, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Municipios.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(uf))
            query = query.Where(m => m.Uf == uf.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Nome.Contains(search.Trim()) || m.CodigoIbge.StartsWith(search.Trim()));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(m => m.Uf).ThenBy(m => m.Nome)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    private static string Norm(string ibge) => new(ibge.Where(char.IsDigit).ToArray());
}

public sealed class NfseMunicipioConfigRepository(FiscalDbContext db) : INfseMunicipioConfigRepository
{
    public Task<NfseMunicipioConfig?> GetByIbgeAsync(string codigoIbge, CancellationToken ct = default)
        => db.NfseMunicipioConfigs.FirstOrDefaultAsync(c => c.CodigoIbge == Norm(codigoIbge), ct);

    public async Task AddAsync(NfseMunicipioConfig config, CancellationToken ct = default)
        => await db.NfseMunicipioConfigs.AddAsync(config, ct);

    public async Task<IReadOnlyList<NfseMunicipioConfig>> ListByIbgesAsync(IEnumerable<string> ibges, CancellationToken ct = default)
    {
        var set = ibges.Select(Norm).ToHashSet();
        return await db.NfseMunicipioConfigs.AsNoTracking().Where(c => set.Contains(c.CodigoIbge)).ToListAsync(ct);
    }

    private static string Norm(string ibge) => new(ibge.Where(char.IsDigit).ToArray());
}

public sealed class NfseAmbienteRepository(FiscalDbContext db) : INfseAmbienteRepository
{
    public async Task<IReadOnlyList<NfseAmbiente>> ListByIbgeAsync(string codigoIbge, CancellationToken ct = default)
        => await db.NfseAmbientes.AsNoTracking()
            .Where(a => a.CodigoIbge == codigoIbge.Trim() || a.CodigoIbge == "*")
            .OrderBy(a => a.Ambiente).ToListAsync(ct);

    public Task<NfseAmbiente?> FindAsync(string codigoIbge, ModoEmissaoNfse modo, AmbienteFiscal ambiente, CancellationToken ct = default)
        => db.NfseAmbientes.FirstOrDefaultAsync(a =>
            a.CodigoIbge == codigoIbge.Trim() && a.Modo == modo && a.Ambiente == ambiente, ct);

    public async Task AddAsync(NfseAmbiente ambiente, CancellationToken ct = default)
        => await db.NfseAmbientes.AddAsync(ambiente, ct);
}
