namespace Fiscal.Application.Emissao;

/// <summary>Interpretação da transmissão (R6: HTTP 200 ≠ autorização).</summary>
public enum ResultadoTransmissao
{
    Autorizado,
    Rejeitado,
    Aguardando,
    Desconhecido,
    ErroTecnico
}

public sealed record TransmissaoResult(
    ResultadoTransmissao Resultado,
    string? CStat,
    string? Motivo,
    string? Protocolo);

/// <summary>Contexto imutável da tentativa (destino fixado — mudança de config não redireciona).</summary>
public sealed record DocumentoContexto(
    Guid DocumentoId,
    Fiscal.Domain.Enums.TipoDocumentoFiscal Tipo,
    Fiscal.Domain.Enums.AmbienteFiscal Ambiente,
    string Serie,
    int Numero,
    string? Chave,
    string SnapshotEmitente,
    string SnapshotDestinatario,
    string SnapshotItens,
    string SnapshotTotais);

/// <summary>
/// Porta do autorizador (Etapa 43: simulador; Etapas 45/47: SEFAZ/NFS-e reais).
/// Implementações NUNCA reenviam após timeout sem consulta (R6).
/// </summary>
public interface IAutorizadorFiscal
{
    string Nome { get; }
    Task<TransmissaoResult> TransmitirAsync(DocumentoContexto ctx, CancellationToken ct);
    Task<TransmissaoResult> ConsultarAsync(DocumentoContexto ctx, CancellationToken ct);
    Task<TransmissaoResult> CancelarAsync(DocumentoContexto ctx, string justificativa, CancellationToken ct);

    /// <summary>Consulta de recibo de lote (padrão: equivale à consulta).</summary>
    Task<TransmissaoResult> ConsultarReciboAsync(DocumentoContexto ctx, string recibo, CancellationToken ct)
        => ConsultarAsync(ctx, ct);
}
