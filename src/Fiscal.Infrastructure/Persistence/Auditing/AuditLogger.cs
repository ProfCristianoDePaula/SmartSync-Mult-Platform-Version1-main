using Fiscal.Application.Auditing;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Infrastructure.Persistence;

namespace Fiscal.Infrastructure.Persistence.Auditing;

/// <summary>
/// Implementação EF do <see cref="IAuditLogger"/>. Persiste SÓ metadados;
/// chamadas nunca devem incluir segredos (R4) — garantido por construção,
/// pois a assinatura só aceita strings curtas de ação/entidade.
/// </summary>
public sealed class AuditLogger(FiscalDbContext dbContext) : IAuditLogger
{
    public async Task LogAsync(
        TenantId? tenantId,
        Guid? userId,
        string action,
        string entity,
        string? entityId = null,
        CancellationToken ct = default)
    {
        await dbContext.AuditLogs.AddAsync(
            AuditLog.Create(tenantId, userId, action, entity, entityId), ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
