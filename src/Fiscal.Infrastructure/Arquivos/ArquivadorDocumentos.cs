using System.Text;
using System.Text.Json;
using Fiscal.Application.Arquivos;
using Fiscal.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Fiscal.Infrastructure.Arquivos;

/// <summary>
/// Arquiva evidências do pipeline (best-effort: nunca quebra o fluxo fiscal).
/// Pasta = chave de acesso (ou id); arquivos: enviado.xml, resposta.json, evento.json.
/// </summary>
public sealed class ArquivadorDocumentos(
    IArmazenamentoFiscal arquivos,
    ILogger<ArquivadorDocumentos> logger)
{
    public async Task ArquivarEnvioAsync(DocumentoFiscal doc, CancellationToken ct = default)
    {
        try
        {
            var pasta = PastaDe(doc);
            if (doc.XmlUltimoEnvio is not null)
                await arquivos.GuardarAsync(doc.TenantId, pasta, "enviado.xml",
                    Encoding.UTF8.GetBytes(doc.XmlUltimoEnvio), ct);
            var resposta = JsonSerializer.Serialize(new
            {
                doc.CStat,
                doc.Motivo,
                doc.Protocolo,
                Status = doc.Status.ToString(),
                em = DateTime.UtcNow
            });
            await arquivos.GuardarAsync(doc.TenantId, pasta, "resposta.json",
                Encoding.UTF8.GetBytes(resposta), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao arquivar documento {DocumentoId} (sem impacto fiscal).", doc.Id.Value);
        }
    }

    public async Task ArquivarEventoAsync(DocumentoFiscal doc, string eventoId, string justificativa, CancellationToken ct = default)
    {
        try
        {
            var evento = JsonSerializer.Serialize(new
            {
                eventoId,
                doc = doc.Id.Value,
                justificativa,
                doc.Protocolo,
                em = DateTime.UtcNow
            });
            await arquivos.GuardarAsync(doc.TenantId, PastaDe(doc), $"evento-{eventoId}.json",
                Encoding.UTF8.GetBytes(evento), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao arquivar evento {EventoId}.", eventoId);
        }
    }

    internal static string PastaDe(DocumentoFiscal doc)
        => string.IsNullOrWhiteSpace(doc.ChaveAcesso) ? doc.Id.Value.ToString("N") : doc.ChaveAcesso;
}
