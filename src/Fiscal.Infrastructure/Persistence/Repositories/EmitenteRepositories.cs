using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence.Repositories;

public sealed class EmitenteFiscalRepository(FiscalDbContext db) : IEmitenteFiscalRepository
{
    public Task<EmitenteFiscal?> GetAsync(TenantId tenantId, Guid emitenteId, CancellationToken ct = default)
        => db.EmitentesFiscais.FirstOrDefaultAsync(e =>
            e.TenantId == tenantId && e.Id == EmitenteFiscalId.From(emitenteId), ct);

    public Task<EmitenteFiscal?> GetByBranchAsync(TenantId tenantId, BranchId branchId, CancellationToken ct = default)
        => db.EmitentesFiscais.FirstOrDefaultAsync(e =>
            e.TenantId == tenantId && e.BranchId == branchId, ct);

    public async Task AddAsync(EmitenteFiscal emitente, CancellationToken ct = default)
        => await db.EmitentesFiscais.AddAsync(emitente, ct);

    public async Task<IReadOnlyList<EmitenteFiscal>> ListAsync(TenantId tenantId, CancellationToken ct = default)
        => await db.EmitentesFiscais.AsNoTracking()
            .Where(e => e.TenantId == tenantId).OrderBy(e => e.Fantasia).ToListAsync(ct);
}

public sealed class ConfiguracaoDocumentoRepository(FiscalDbContext db) : IConfiguracaoDocumentoRepository
{
    public Task<ConfiguracaoDocumento?> FindAsync(EmitenteFiscalId emitenteId, TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, CancellationToken ct = default)
        => db.ConfiguracoesDocumento.FirstOrDefaultAsync(c =>
            c.EmitenteId == emitenteId && c.Tipo == tipo && c.Ambiente == ambiente, ct);

    public async Task<IReadOnlyList<ConfiguracaoDocumento>> ListByEmitenteAsync(EmitenteFiscalId emitenteId, CancellationToken ct = default)
        => await db.ConfiguracoesDocumento.AsNoTracking()
            .Where(c => c.EmitenteId == emitenteId).ToListAsync(ct);

    public async Task AddAsync(ConfiguracaoDocumento config, CancellationToken ct = default)
        => await db.ConfiguracoesDocumento.AddAsync(config, ct);
}

public sealed class ConcessaoRepository(FiscalDbContext db) : IConcessaoRepository
{
    public Task<ConcessaoUnidade?> GetAsync(TenantId tenantId, Guid concessaoId, CancellationToken ct = default)
        => db.ConcessoesUnidade.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.Id == ConcessaoUnidadeId.From(concessaoId), ct);

    public Task<ConcessaoUnidade?> FindAsync(TenantId tenantId, BranchId branchId, Guid userId, PapelUnidade papel, CancellationToken ct = default)
        => db.ConcessoesUnidade.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.BranchId == branchId && c.UserId == userId && c.Papel == papel, ct);

    public async Task AddAsync(ConcessaoUnidade concessao, CancellationToken ct = default)
        => await db.ConcessoesUnidade.AddAsync(concessao, ct);

    public async Task<IReadOnlyList<ConcessaoUnidade>> ListAsync(TenantId tenantId, BranchId? branchId, CancellationToken ct = default)
    {
        var query = db.ConcessoesUnidade.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (branchId is not null)
            query = query.Where(c => c.BranchId == branchId);
        return await query.OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
    }
}
