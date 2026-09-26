using Estoque.Domain.Common;
using FluentValidation;

namespace Estoque.Application.FiscalBridge;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record EmitirNotaRequest(TipoDocumentoFiscalBridge Tipo)
{
    public EmitirNotaRequest WithContext(TenantId tenantId, Guid userId) => this with { TenantId = tenantId, UserId = userId };
    public TenantId TenantId { get; init; }
    public Guid UserId { get; init; }
}

/// <summary>55 = NF-e, 65 = NFC-e, 200 = NFS-e.</summary>
public enum TipoDocumentoFiscalBridge
{
    NFe55 = 55,
    NFCe65 = 65,
    NFSe200 = 200
}

public sealed record DocumentoEmitidoDto(
    Guid Id,
    int Tipo,
    int Status,
    string Serie,
    int Numero,
    string? ChaveAcesso,
    string? Protocolo,
    bool CriadoAgora);

public sealed record NotaFiscalVendaDto(
    Guid VendaId,
    IReadOnlyList<DocumentoEmitidoDto> Documentos,
    IReadOnlyList<string> AcoesSugeridas);

// ---------------------------------------------------------------------------
// Tabela FormaPagto → tPag (versionada; leiaute 4.00 — reconfirmar, F0-31)
// ---------------------------------------------------------------------------

public static class FormaPagtoTpags
{
    private static readonly Dictionary<string, string> Mapa = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pix"] = "17",
        ["Transferência"] = "18",
        ["Transferencia"] = "18",
        ["Depósito"] = "16",
        ["Deposito"] = "16",
        ["Cartão de Débito"] = "04",
        ["Cartao de Debito"] = "04",
        ["Cartão de Crédito"] = "03",
        ["Cartao de Credito"] = "03",
    };

    public static string Para(string descricao)
        => Mapa.TryGetValue((descricao ?? "").Trim(), out var tpag) ? tpag : "99";
}

// ---------------------------------------------------------------------------
// Interfaces
// ---------------------------------------------------------------------------

public interface IFiscalBridgeClient
{
    Task<EmitenteFiscalView?> ObterEmitentePorFilialAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<ProdutoFiscalView>> ObterPendenciasProdutosAsync(TenantId tenantId, IReadOnlyList<Guid> produtoIds, CancellationToken ct = default);
    Task<DocumentoFiscalView> CriarDocumentoAsync(TenantId tenantId, object body, string idempotencyKey, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentoFiscalView>> ListarPorVendaAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default);
}

public sealed record EmitenteFiscalView(Guid Id, bool EmissaoAutomaticaVenda);
public sealed record ProdutoFiscalView(Guid ProdutoId, int Tipo, bool ProntoNFe, bool ProntoNFSe, IReadOnlyList<string> Pendencias);
public sealed record DocumentoFiscalView(Guid Id, int Tipo, int Status, string Serie, int Numero, string? ChaveAcesso, string? Protocolo, bool CriadoAgora);

public interface INotaFiscalBridgeService
{
    Task<NotaFiscalVendaDto> EmitirNotaAsync(Guid vendaId, EmitirNotaRequest request, CancellationToken ct = default);
    Task<NotaFiscalVendaDto> ConsultarAsync(Guid vendaId, TenantId tenantId, CancellationToken ct = default);
    Task AvaliarEmissaoAutomaticaAsync(Guid vendaId, TenantId tenantId, CancellationToken ct = default);
}

public sealed class EmitirNotaRequestValidator : AbstractValidator<EmitirNotaRequest>
{
    public EmitirNotaRequestValidator()
    {
        RuleFor(x => x.Tipo).IsInEnum();
    }
}
