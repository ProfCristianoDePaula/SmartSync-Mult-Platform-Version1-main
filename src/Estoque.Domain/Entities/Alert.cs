using Estoque.Domain.Common;
using Estoque.Domain.Enums;

namespace Estoque.Domain.Entities;

/// <summary>
/// Alerta gerado pelas políticas/jobs (ruptura, excesso, validade, outlet).
/// Idempotência: os geradores deduplicam por (tenant, tipo, referência) com
/// alerta não reconhecido existente.
/// </summary>
public sealed class Alert : Entity<AlertId>
{
    public TenantId TenantId { get; private set; }
    public BranchId? BranchId { get; private set; }
    public ProductId? ProductId { get; private set; }
    public LotId? LotIdRef { get; private set; }

    public AlertType Type { get; private set; }
    public AlertSeverity Severity { get; private set; }
    public string Message { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Dedupe: data (UTC, dia) em que o alerta foi (re)gerado.</summary>
    public DateOnly GeneratedOn { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    private Alert() { }

    private Alert(AlertId id, TenantId tenantId, Guid? branchId, ProductId? productId,
        Common.LotId? lotId, AlertType type, AlertSeverity severity, string message)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Mensagem do alerta é obrigatória.", nameof(message));

        TenantId = tenantId;
        BranchId = branchId is null ? null : Common.BranchId.From(branchId.Value);
        ProductId = productId;
        LotIdRef = lotId;
        Type = type;
        Severity = severity;
        Message = message.Trim();
        CreatedAtUtc = DateTime.UtcNow;
        GeneratedOn = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public static Alert Create(TenantId tenantId, Guid? branchId, Guid? productId,
        Guid? lotId, AlertType type, AlertSeverity severity, string message)
        => new(
            AlertId.New(), tenantId, branchId,
            productId is null ? null : Common.ProductId.From(productId.Value),
            lotId is null ? null : Common.LotId.From(lotId.Value),
            type, severity, message);

    public void Acknowledge(Guid userId)
    {
        if (AcknowledgedAtUtc is not null)
            return;
        AcknowledgedAtUtc = DateTime.UtcNow;
        AcknowledgedByUserId = userId;
    }
}
