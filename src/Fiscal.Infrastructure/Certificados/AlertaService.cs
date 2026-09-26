using Fiscal.Application.Certificados;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Infrastructure.Persistence;

namespace Fiscal.Infrastructure.Certificados;

public sealed class AlertaService(IAlertaRepository alertas, FiscalDbContext db) : IAlertaService
{
    public async Task<IReadOnlyList<AlertaDto>> ListAsync(TenantId? tenantId, bool apenasNaoLidas, CancellationToken ct = default)
    {
        var items = await alertas.ListAsync(tenantId, apenasNaoLidas, ct);
        return items.Select(a => new AlertaDto(a.Id.Value, a.Tipo, a.Mensagem, a.EntityId, a.Lida, a.CreatedAtUtc)).ToList();
    }

    public async Task<bool> MarcarLidaAsync(TenantId? tenantId, Guid id, CancellationToken ct = default)
    {
        var alerta = await alertas.GetAsync(tenantId, id, ct);
        if (alerta is null) return false;
        alerta.MarcarLida();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
