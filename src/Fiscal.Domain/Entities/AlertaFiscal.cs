using Fiscal.Domain.Common;

namespace Fiscal.Domain.Entities;

/// <summary>Alerta operacional do Fiscal (ex.: certificado expirando). Sem segredos.</summary>
public sealed class AlertaFiscal : Entity<AlertaFiscalId>
{
    public TenantId? TenantId { get; private set; }
    public string Tipo { get; private set; } = null!;
    public string Mensagem { get; private set; } = null!;
    public string? EntityId { get; private set; }
    public bool Lida { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LidaEm { get; private set; }

    private AlertaFiscal() { }

    private AlertaFiscal(AlertaFiscalId id, TenantId? tenantId, string tipo, string mensagem, string? entityId)
        : base(id)
    {
        TenantId = tenantId;
        Tipo = tipo;
        Mensagem = mensagem;
        EntityId = entityId;
        Lida = false;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static AlertaFiscal Create(TenantId? tenantId, string tipo, string mensagem, string? entityId = null)
    {
        if (string.IsNullOrWhiteSpace(tipo)) throw new ArgumentException("Tipo obrigatório.", nameof(tipo));
        if (string.IsNullOrWhiteSpace(mensagem)) throw new ArgumentException("Mensagem obrigatória.", nameof(mensagem));
        return new(AlertaFiscalId.New(), tenantId, tipo.Trim(), mensagem.Trim(),
            string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim());
    }

    public void MarcarLida()
    {
        if (Lida) return;
        Lida = true;
        LidaEm = DateTime.UtcNow;
    }
}
