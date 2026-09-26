using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Documento fiscal (aggregate): tipo, ambiente, snapshots IMUTÁVEIS
/// (emitente/destinatário/itens/totais em JSON), série, número, chave,
/// protocolo, origem (venda/pedido), idempotência. R9: jamais apagado;
/// cancelar é evento, não DELETE (sem SoftDelete aqui).
/// </summary>
public sealed class DocumentoFiscal : Entity<DocumentoFiscalId>
{
    public TenantId TenantId { get; private set; }
    public EmitenteFiscalId EmitenteId { get; private set; }
    public TipoDocumentoFiscal Tipo { get; private set; }
    public AmbienteFiscal Ambiente { get; private set; }
    public StatusDocumentoFiscal Status { get; private set; }
    public string Serie { get; private set; } = null!;
    public int Numero { get; private set; }
    public string? ChaveAcesso { get; private set; }
    public string? Protocolo { get; private set; }
    public string? CStat { get; private set; }
    public string? Motivo { get; private set; }
    public Guid? OrigemVendaId { get; private set; }
    public Guid? OrigemPedidoId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public string SnapshotEmitente { get; private set; } = null!;
    public string SnapshotDestinatario { get; private set; } = null!;
    public string SnapshotItens { get; private set; } = null!;
    public string SnapshotTotais { get; private set; } = null!;
    public int Tentativas { get; private set; }
    public DateTime? ProximaTentativaEm { get; private set; }
    public string? XmlUltimoEnvio { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private static readonly Dictionary<StatusDocumentoFiscal, StatusDocumentoFiscal[]> Transicoes = new()
    {
        [StatusDocumentoFiscal.Pendente] = [StatusDocumentoFiscal.Validando, StatusDocumentoFiscal.ErroTecnico],
        [StatusDocumentoFiscal.Validando] = [StatusDocumentoFiscal.Assinado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.ErroTecnico],
        [StatusDocumentoFiscal.Assinado] = [StatusDocumentoFiscal.EnvioPendente, StatusDocumentoFiscal.ErroTecnico],
        [StatusDocumentoFiscal.EnvioPendente] = [StatusDocumentoFiscal.AguardandoProcessamento, StatusDocumentoFiscal.ResultadoDesconhecido, StatusDocumentoFiscal.ErroTecnico],
        [StatusDocumentoFiscal.AguardandoProcessamento] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.ResultadoDesconhecido, StatusDocumentoFiscal.ErroTecnico],
        [StatusDocumentoFiscal.ResultadoDesconhecido] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.AguardandoProcessamento, StatusDocumentoFiscal.ErroTecnico],
        [StatusDocumentoFiscal.Autorizado] = [StatusDocumentoFiscal.Cancelado],
        [StatusDocumentoFiscal.Rejeitado] = [StatusDocumentoFiscal.Validando],
        [StatusDocumentoFiscal.ErroTecnico] = [StatusDocumentoFiscal.Validando, StatusDocumentoFiscal.EnvioPendente],
        [StatusDocumentoFiscal.Cancelado] = [],
    };

    private DocumentoFiscal() { }

    private DocumentoFiscal(
        DocumentoFiscalId id, TenantId tenantId, EmitenteFiscalId emitenteId,
        TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, string serie, int numero,
        string? chaveAcesso, Guid? origemVendaId, Guid? origemPedidoId,
        string idempotencyKey, string requestHash,
        string snapshotEmitente, string snapshotDestinatario,
        string snapshotItens, string snapshotTotais)
        : base(id)
    {
        TenantId = tenantId;
        EmitenteId = emitenteId;
        Tipo = tipo;
        Ambiente = ambiente;
        Status = StatusDocumentoFiscal.Pendente;
        Serie = serie;
        Numero = numero;
        ChaveAcesso = chaveAcesso;
        OrigemVendaId = origemVendaId;
        OrigemPedidoId = origemPedidoId;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        SnapshotEmitente = snapshotEmitente;
        SnapshotDestinatario = snapshotDestinatario;
        SnapshotItens = snapshotItens;
        SnapshotTotais = snapshotTotais;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public static DocumentoFiscal Criar(
        TenantId tenantId, EmitenteFiscalId emitenteId,
        TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, string serie, int numero,
        string? chaveAcesso, Guid? origemVendaId, Guid? origemPedidoId,
        string idempotencyKey, string requestHash,
        string snapshotEmitente, string snapshotDestinatario,
        string snapshotItens, string snapshotTotais)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Chave de idempotência obrigatória.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(requestHash))
            throw new ArgumentException("Hash do pedido obrigatório.", nameof(requestHash));
        return new DocumentoFiscal(DocumentoFiscalId.New(), tenantId, emitenteId, tipo,
            ambiente, serie, numero, chaveAcesso, origemVendaId, origemPedidoId,
            idempotencyKey.Trim(), requestHash.Trim(), snapshotEmitente,
            snapshotDestinatario, snapshotItens, snapshotTotais);
    }

    public void TransicionarPara(StatusDocumentoFiscal novo)
    {
        if (!Transicoes[Status].Contains(novo))
            throw new BusinessRuleViolationException(
                $"Transição {Status} → {novo} não permitida.");
        Status = novo;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Registra retorno do autorizador (cStat/motivo/protocolo).</summary>
    public void RegistrarRetorno(string cStat, string? motivo, string? protocolo)
    {
        CStat = cStat;
        Motivo = motivo;
        Protocolo = protocolo;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Agenda retry com backoff (só onde seguro — nunca após envio).</summary>
    public void AgendarTentativa(DateTime proxima)
    {
        Tentativas++;
        ProximaTentativaEm = proxima;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void GuardarXmlEnviado(string xml)
    {
        XmlUltimoEnvio = xml;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Nova numeração após rejeição/correção: o número antigo NÃO é reutilizado
    /// (vai para análise/inutilização). Snapshots permanecem intactos.
    /// </summary>
    public void Renumerar(int novoNumero, string? novaChave)
    {
        if (Status != StatusDocumentoFiscal.Rejeitado && Status != StatusDocumentoFiscal.ErroTecnico)
            throw new BusinessRuleViolationException(
                "Renumerar só a partir de Rejeitado ou ErroTecnico.");
        if (novoNumero <= Numero)
            throw new ArgumentException("Novo número deve ser maior que o atual.", nameof(novoNumero));
        Numero = novoNumero;
        ChaveAcesso = novaChave;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
