using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;

namespace Fiscal.Application.Emissao;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record DocumentoDto(
    Guid Id,
    Guid EmitenteId,
    TipoDocumentoFiscal Tipo,
    AmbienteFiscal Ambiente,
    StatusDocumentoFiscal Status,
    string Serie,
    int Numero,
    string? ChaveAcesso,
    string? Protocolo,
    string? CStat,
    string? Motivo,
    Guid? OrigemVendaId,
    int Tentativas,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record EventoDto(
    Guid Id,
    Guid DocumentoId,
    TipoEventoFiscal Tipo,
    string? CodigoEvento,
    string Status,
    string? Protocolo);

// ---------------------------------------------------------------------------
// Request de criação (pipeline)
// ---------------------------------------------------------------------------

public sealed record CriarDocumentoRequest(
    Guid EmitenteId,
    TipoDocumentoFiscal Tipo,
    string? Serie,
    Guid? OrigemVendaId,
    Guid? OrigemPedidoId,
    string SnapshotDestinatario,
    string SnapshotItens,
    string SnapshotTotais,
    string? ObservacaoPedido)
{
    public CriarDocumentoRequest WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record CancelarDocumentoRequest(string Justificativa);

public sealed class CriarDocumentoRequestValidator : AbstractValidator<CriarDocumentoRequest>
{
    public CriarDocumentoRequestValidator()
    {
        RuleFor(x => x.EmitenteId).NotEqual(Guid.Empty);
        RuleFor(x => x.Tipo).IsInEnum()
            .Must(t => t is TipoDocumentoFiscal.NFe55 or TipoDocumentoFiscal.NFCe65 or TipoDocumentoFiscal.NFSe)
            .WithMessage("Tipo deve ser NFe55, NFCe65 ou NFSe.");
        RuleFor(x => x.SnapshotDestinatario).NotEmpty();
        RuleFor(x => x.SnapshotItens).NotEmpty().Must(s => s.Trim() != "[]")
            .WithMessage("Documento exige ao menos um item.");
        RuleFor(x => x.SnapshotTotais).NotEmpty();
    }
}

public sealed class CancelarDocumentoRequestValidator : AbstractValidator<CancelarDocumentoRequest>
{
    public CancelarDocumentoRequestValidator()
    {
        RuleFor(x => x.Justificativa).NotEmpty().MaximumLength(1000)
            .WithMessage("Justificativa do cancelamento é obrigatória.");
    }
}

// ---------------------------------------------------------------------------
// Serviços do pipeline
// ---------------------------------------------------------------------------

public sealed record CriacaoDocumentoResult(DocumentoDto Documento, bool CriadoNovo);

public interface IEmissaoService
{
    Task<CriacaoDocumentoResult> CriarAsync(CriarDocumentoRequest request, string idempotencyKey, Guid userId, string role, CancellationToken ct = default);
    Task<DocumentoDto?> GetByIdAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentoDto>> ListAsync(TenantId tenantId, StatusDocumentoFiscal? status, int page, int pageSize, CancellationToken ct = default);
    Task<EventoDto> SolicitarCancelamentoAsync(TenantId tenantId, Guid id, CancelarDocumentoRequest request, Guid userId, string role, CancellationToken ct = default);
    Task<DocumentoDto> ConciliarAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<string?> ObterXmlAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentoDto>> ListarPorVendaAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default);
}

public interface IConciliacaoService
{
    /// <summary>Concilia um documento (consulta o autorizador; nunca retransmite cegamente).</summary>
    Task ConciliarDocumentoAsync(Guid documentoId, CancellationToken ct = default);
    /// <summary>Varredura de conciliação (desconhecidos + processamento preso + eventos pendentes).</summary>
    Task<int> VarrerAsync(CancellationToken ct = default);
}
