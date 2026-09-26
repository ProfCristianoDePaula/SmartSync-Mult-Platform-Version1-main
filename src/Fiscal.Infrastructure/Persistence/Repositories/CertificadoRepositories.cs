using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence.Repositories;

public sealed class CertificadoRepository(FiscalDbContext db) : ICertificadoRepository
{
    public Task<CertificadoDigital?> GetAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
        => db.CertificadosDigitais.FirstOrDefaultAsync(c =>
            c.TenantId == tenantId && c.Id == CertificadoDigitalId.From(id), ct);

    public async Task<IReadOnlyList<CertificadoDigital>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default)
        => await db.CertificadosDigitais.AsNoTracking()
            .Where(c => c.TenantId == tenantId).OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<CertificadoDigital>> ListAtivosAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default)
        => await db.CertificadosDigitais
            .Where(c => c.TenantId == tenantId && c.BranchId == branchId && c.Status == CertificadoStatus.Ativo)
            .ToListAsync(ct);

    public async Task AddAsync(CertificadoDigital certificado, CancellationToken ct = default)
        => await db.CertificadosDigitais.AddAsync(certificado, ct);
}

public sealed class AlertaRepository(FiscalDbContext db) : IAlertaRepository
{
    public Task<AlertaFiscal?> GetAsync(TenantId? tenantId, Guid id, CancellationToken ct = default)
    {
        var query = db.AlertasFiscais.AsQueryable();
        if (tenantId is not null)
            query = query.Where(a => a.TenantId == tenantId);
        return query.FirstOrDefaultAsync(a => a.Id == AlertaFiscalId.From(id), ct);
    }

    public Task<bool> ExisteNaoLidoAsync(TenantId? tenantId, string tipo, string? entityId, CancellationToken ct = default)
    {
        var query = db.AlertasFiscais.Where(a => !a.Lida && a.Tipo == tipo && a.EntityId == entityId);
        if (tenantId is not null)
            query = query.Where(a => a.TenantId == tenantId);
        return query.AnyAsync(ct);
    }

    public async Task AddAsync(AlertaFiscal alerta, CancellationToken ct = default)
        => await db.AlertasFiscais.AddAsync(alerta, ct);

    public async Task<IReadOnlyList<AlertaFiscal>> ListAsync(TenantId? tenantId, bool apenasNaoLidas, CancellationToken ct = default)
    {
        var query = db.AlertasFiscais.AsNoTracking().AsQueryable();
        if (tenantId is not null)
            query = query.Where(a => a.TenantId == tenantId);
        if (apenasNaoLidas)
            query = query.Where(a => !a.Lida);
        return await query.OrderByDescending(a => a.CreatedAtUtc).Take(200).ToListAsync(ct);
    }
}
