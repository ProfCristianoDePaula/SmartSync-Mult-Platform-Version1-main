using Fiscal.Domain.Common;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Registro de auditoria append-only: quem, quando, ação, entidade e tenant.
/// NUNCA gravar segredos (R4): PFX, senhas, CSC e chaves não passam por aqui —
/// o <see cref="Fiscal.Application.Auditing.IAuditLogger"/> só recebe metadados.
/// Sem soft delete: linhas de auditoria jamais são apagadas (R9).
/// </summary>
public sealed class AuditLog : Entity<AuditLogId>
{
    public TenantId? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string Entity { get; private set; } = null!;
    public string? EntityId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private AuditLog() { }

    private AuditLog(
        AuditLogId id,
        TenantId? tenantId,
        Guid? userId,
        string action,
        string entity,
        string? entityId,
        DateTime occurredAtUtc)
        : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        SetAction(action);
        SetEntity(entity);
        EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim();
        OccurredAtUtc = occurredAtUtc;
    }

    public static AuditLog Create(
        TenantId? tenantId,
        Guid? userId,
        string action,
        string entity,
        string? entityId = null)
        => new(AuditLogId.New(), tenantId, userId, action, entity, entityId, DateTime.UtcNow);

    private void SetAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Ação de auditoria é obrigatória.", nameof(action));
        if (action.Trim().Length > 200)
            throw new ArgumentException("Ação de auditoria excede 200 caracteres.", nameof(action));
        Action = action.Trim();
    }

    private void SetEntity(string entity)
    {
        if (string.IsNullOrWhiteSpace(entity))
            throw new ArgumentException("Entidade auditada é obrigatória.", nameof(entity));
        if (entity.Trim().Length > 200)
            throw new ArgumentException("Entidade auditada excede 200 caracteres.", nameof(entity));
        Entity = entity.Trim();
    }
}
