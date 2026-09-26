using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>Evento vinculado ao documento (cancelamento, correção, ...). Append-only.</summary>
public sealed class EventoFiscal : Entity<EventoFiscalId>
{
    public TenantId TenantId { get; private set; }
    public DocumentoFiscalId DocumentoId { get; private set; }
    public TipoEventoFiscal Tipo { get; private set; }
    public string? CodigoEvento { get; private set; }
    public string? Justificativa { get; private set; }
    public string? Protocolo { get; private set; }
    public string Status { get; private set; } = "Pendente";
    public DateTime CreatedAtUtc { get; private set; }

    private EventoFiscal() { }

    private EventoFiscal(
        EventoFiscalId id, TenantId tenantId, DocumentoFiscalId documentoId,
        TipoEventoFiscal tipo, string? codigoEvento, string? justificativa)
        : base(id)
    {
        TenantId = tenantId;
        DocumentoId = documentoId;
        Tipo = tipo;
        CodigoEvento = codigoEvento;
        Justificativa = justificativa;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static EventoFiscal Criar(
        TenantId tenantId, DocumentoFiscalId documentoId,
        TipoEventoFiscal tipo, string? codigoEvento, string? justificativa)
        => new(EventoFiscalId.New(), tenantId, documentoId, tipo, codigoEvento, justificativa);

    public void Concluir(string status, string? protocolo)
    {
        Status = status;
        Protocolo = protocolo;
    }
}
