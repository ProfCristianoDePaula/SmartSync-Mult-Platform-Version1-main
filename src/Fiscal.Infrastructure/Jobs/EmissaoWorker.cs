using System.Data.Common;
using Fiscal.Application.Documentos;
using Fiscal.Application.Emissao;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Emissao;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fiscal.Infrastructure.Jobs;

/// <summary>
/// Worker de emissão (Fiscal-8): reivindica documentos `EnvioPendente` com
/// `FOR UPDATE SKIP LOCKED` (várias instâncias), transmite pelo autorizador
/// da etapa e concilia desconhecidos. A intenção já está persistida antes da
/// chamada externa (R6); timeout ⇒ ResultadoDesconhecido ⇒ consulta.
/// </summary>
public sealed class EmissaoWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<EmissaoWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);
    private const int Lote = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessarAsync(stoppingToken); }
            catch (Exception ex)
            {
                logger.LogError(ex, "EmissaoWorker falhou; nova tentativa no próximo ciclo.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessarAsync(CancellationToken ct)
    {
        var ids = await ReivindicarAsync(ct);
        if (ids.Count == 0)
        {
            await ConciliarAsync(ct);
            return;
        }

        foreach (var id in ids)
        {
            try { await TransmitirAsync(id, ct); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao transmitir documento {DocumentoId}.", id);
            }
        }

        await ConciliarAsync(ct);
    }

    private async Task<IReadOnlyList<Guid>> ReivindicarAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FiscalDbContext>();

        var conn = db.Database.GetDbConnection();
        var wasClosed = conn.State == System.Data.ConnectionState.Closed;
        if (wasClosed) await conn.OpenAsync(ct);
        try
        {
            await using var tx = await conn.BeginTransactionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE documentos_fiscais
                   SET status = 5, updated_at_utc = now()
                 WHERE id IN (
                    SELECT id FROM documentos_fiscais
                     WHERE status = 4
                       AND (proxima_tentativa_em IS NULL OR proxima_tentativa_em <= now())
                     ORDER BY updated_at_utc
                     LIMIT 20
                     FOR UPDATE SKIP LOCKED
                 )
                RETURNING id
                """;
            var ids = new List<Guid>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ids.Add(reader.GetGuid(0));
            await reader.DisposeAsync();
            await tx.CommitAsync(ct);
            return ids;
        }
        finally
        {
            if (wasClosed) await conn.CloseAsync();
        }
    }

    private async Task TransmitirAsync(Guid id, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FiscalDbContext>();
        var selector = scope.ServiceProvider.GetRequiredService<Fiscal.Infrastructure.Sefaz.IAutorizadorSelector>();
        var audit = scope.ServiceProvider.GetRequiredService<Fiscal.Application.Auditing.IAuditLogger>();

        var doc = await db.DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == DocumentoFiscalId.From(id), ct);
        if (doc is null || doc.Status != StatusDocumentoFiscal.AguardandoProcessamento)
            return;

        var autorizador = await selector.ParaAsync(doc.TenantId, doc.EmitenteId, doc.Tipo, doc.Ambiente, ct);

        var ctx = ConciliacaoService.ContextoDe(doc);
        doc.GuardarXmlEnviado(SimuladorAutorizador.XmlSimulado(ctx));

        Fiscal.Application.Emissao.TransmissaoResult resultado;
        try
        {
            resultado = await autorizador.TransmitirAsync(ctx, ct);
        }
        catch (TimeoutException)
        {
            // Resultado desconhecido (R6): consulta na conciliação, sem reenvio.
            doc.TransicionarPara(StatusDocumentoFiscal.ResultadoDesconhecido);
            doc.AgendarTentativa(DateTime.UtcNow.AddMinutes(2));
            await db.SaveChangesAsync(ct);
            return;
        }
        catch (Exception)
        {
            doc.TransicionarPara(StatusDocumentoFiscal.ErroTecnico);
            doc.AgendarTentativa(DateTime.UtcNow.AddMinutes(5));
            await db.SaveChangesAsync(ct);
            return;
        }

        AplicarResultado(doc, resultado);
        await db.SaveChangesAsync(ct);

        var arquivador = scope.ServiceProvider
            .GetRequiredService<Fiscal.Infrastructure.Arquivos.ArquivadorDocumentos>();
        await arquivador.ArquivarEnvioAsync(doc, ct);

        if (doc.Status == StatusDocumentoFiscal.Autorizado)
            await audit.LogAsync(doc.TenantId, null, "fiscal.documento.autorizado", "DocumentoFiscal",
                doc.Id.Value.ToString(), ct);
    }

    private async Task ConciliarAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var conciliacao = scope.ServiceProvider.GetRequiredService<IConciliacaoService>();
        await conciliacao.VarrerAsync(ct);
    }

    internal static void AplicarResultado(DocumentoFiscal doc, Fiscal.Application.Emissao.TransmissaoResult resultado)
    {
        switch (resultado.Resultado)
        {
            case Fiscal.Application.Emissao.ResultadoTransmissao.Autorizado:
                doc.RegistrarRetorno(resultado.CStat ?? "", resultado.Motivo, resultado.Protocolo);
                doc.TransicionarPara(StatusDocumentoFiscal.Autorizado);
                break;
            case Fiscal.Application.Emissao.ResultadoTransmissao.Rejeitado:
                // HTTP 200 com rejeição NUNCA vira autorização (R6).
                doc.RegistrarRetorno(resultado.CStat ?? "", resultado.Motivo, resultado.Protocolo);
                doc.TransicionarPara(StatusDocumentoFiscal.Rejeitado);
                break;
            case Fiscal.Application.Emissao.ResultadoTransmissao.Aguardando:
                if (resultado.Protocolo is not null)
                    doc.RegistrarRetorno(resultado.CStat ?? "", resultado.Motivo, resultado.Protocolo);
                doc.AgendarTentativa(DateTime.UtcNow.AddMinutes(2));
                break;
            default:
                doc.TransicionarPara(StatusDocumentoFiscal.ResultadoDesconhecido);
                doc.AgendarTentativa(DateTime.UtcNow.AddMinutes(2));
                break;
        }
    }
}
