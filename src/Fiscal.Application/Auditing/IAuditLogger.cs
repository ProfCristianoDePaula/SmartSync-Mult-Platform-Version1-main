using Fiscal.Domain.Common;

namespace Fiscal.Application.Auditing;

/// <summary>
/// Auditoria de ações sensíveis do Fiscal. Recebe SÓ metadados —
/// NUNCA segredos (R4): PFX, senhas, CSC e chaves não passam por aqui.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        TenantId? tenantId,
        Guid? userId,
        string action,
        string entity,
        string? entityId = null,
        CancellationToken ct = default);
}
