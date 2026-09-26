using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence.Repositories;

public sealed class ProdutoFiscalRepository(FiscalDbContext db) : IProdutoFiscalRepository
{
    public Task<ProdutoFiscal?> GetAsync(TenantId tenantId, Guid produtoId, CancellationToken ct = default)
        => db.ProdutosFiscais.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProdutoId == produtoId, ct);

    public async Task AddAsync(ProdutoFiscal produto, CancellationToken ct = default)
        => await db.ProdutosFiscais.AddAsync(produto, ct);
}

public sealed class ClienteFiscalRepository(FiscalDbContext db) : IClienteFiscalRepository
{
    public Task<ClienteFiscal?> GetAsync(TenantId tenantId, Guid clienteId, CancellationToken ct = default)
        => db.ClientesFiscais.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ClienteId == clienteId, ct);

    public async Task AddAsync(ClienteFiscal cliente, CancellationToken ct = default)
        => await db.ClientesFiscais.AddAsync(cliente, ct);
}

public sealed class NaturezaRepository(FiscalDbContext db) : INaturezaRepository
{
    public Task<NaturezaOperacao?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
        => db.NaturezasOperacao.FirstOrDefaultAsync(n =>
            n.TenantId == tenantId && n.Id == NaturezaOperacaoId.From(id), ct);

    public Task<NaturezaOperacao?> GetByCodigoAsync(TenantId tenantId, string codigo, CancellationToken ct = default)
        => db.NaturezasOperacao.FirstOrDefaultAsync(n =>
            n.TenantId == tenantId && n.Codigo == codigo.Trim().ToUpperInvariant(), ct);

    public async Task AddAsync(NaturezaOperacao natureza, CancellationToken ct = default)
        => await db.NaturezasOperacao.AddAsync(natureza, ct);

    public async Task<IReadOnlyList<NaturezaOperacao>> ListAsync(TenantId tenantId, CancellationToken ct = default)
        => await db.NaturezasOperacao.AsNoTracking()
            .Where(n => n.TenantId == tenantId).OrderBy(n => n.Codigo).ToListAsync(ct);
}
