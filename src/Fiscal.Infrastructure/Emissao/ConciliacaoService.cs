using Fiscal.Application.Auditing;
using Fiscal.Application.Documentos;
using Fiscal.Application.Emissao;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Emissao;

/// <summary>
/// Conciliação (R6): consulta o autorizador antes de qualquer reenvio.
/// Nunca retransmite cegamente; timeout ⇒ ResultadoDesconhecido ⇒ consulta.
/// </summary>
public sealed class ConciliacaoService(
    IDocumentoFiscalRepository documentos,
    FiscalDbContext db,
    IAuditLogger audit,
    Fiscal.Infrastructure.Sefaz.IAutorizadorSelector selector,
    Fiscal.Infrastructure.Arquivos.ArquivadorDocumentos arquivador) : IConciliacaoService
{
    public async Task ConciliarDocumentoAsync(Guid documentoId, CancellationToken ct = default)
    {
        var doc = await db.DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == DocumentoFiscalId.From(documentoId), ct);
        if (doc is null) return;

        var autorizador = await selector.ParaAsync(doc.TenantId, doc.EmitenteId, doc.Tipo, doc.Ambiente, ct);

        // Eventos de cancelamento pendentes primeiro.
        var pendentes = await db.EventosFiscais
            .Where(e => e.DocumentoId == doc.Id && e.Status == "Pendente"
                && e.Tipo == TipoEventoFiscal.Cancelamento)
            .ToListAsync(ct);

        foreach (var evento in pendentes)
            await ProcessarCancelamentoAsync(doc, evento, autorizador, ct);

        // Recarrega (o cancelamento pode ter mudado o status).
        doc = await db.DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == DocumentoFiscalId.From(documentoId), ct);
        if (doc is null) return;

        if (doc.Status is StatusDocumentoFiscal.ResultadoDesconhecido
            or StatusDocumentoFiscal.AguardandoProcessamento)
        {
            TransmissaoResult consulta;
            try
            {
                var authz = await selector.ParaAsync(doc.TenantId, doc.EmitenteId, doc.Tipo, doc.Ambiente, ct);
                var ctx = ContextoDe(doc);
                consulta = EhRecibo(doc.Protocolo)
                    ? await authz.ConsultarReciboAsync(ctx, doc.Protocolo!, ct)
                    : await authz.ConsultarAsync(ctx, ct);
            }
            catch
            {
                return; // mantém desconhecido; próxima varredura tenta de novo
            }

            AplicarConsulta(doc, consulta);
            await db.SaveChangesAsync(ct);
            if (doc.Status == StatusDocumentoFiscal.Autorizado)
                await arquivador.ArquivarEnvioAsync(doc, ct);
        }
    }

    public async Task<int> VarrerAsync(CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow.AddMinutes(-10);
        var ids = await db.DocumentosFiscais
            .Where(d => (d.Status == StatusDocumentoFiscal.ResultadoDesconhecido
                    && (d.ProximaTentativaEm == null || d.ProximaTentativaEm <= DateTime.UtcNow))
                || (d.Status == StatusDocumentoFiscal.AguardandoProcessamento && d.UpdatedAtUtc <= limite))
            .Select(d => d.Id.Value)
            .Take(50)
            .ToListAsync(ct);

        var eventos = await db.EventosFiscais
            .Where(e => e.Status == "Pendente" && e.Tipo == TipoEventoFiscal.Cancelamento)
            .Select(e => e.DocumentoId.Value)
            .Take(50)
            .ToListAsync(ct);

        var total = 0;
        foreach (var id in ids.Union(eventos).Distinct())
        {
            await ConciliarDocumentoAsync(id, ct);
            total++;
        }
        return total;
    }

    private async Task ProcessarCancelamentoAsync(
        DocumentoFiscal doc, EventoFiscal evento, IAutorizadorFiscal autorizador, CancellationToken ct)
    {
        TransmissaoResult resultado;
        try
        {
            resultado = await autorizador.CancelarAsync(ContextoDe(doc), evento.Justificativa ?? "", ct);
        }
        catch (TimeoutException)
        {
            return; // resultado incerto: consulta na próxima varredura, sem apagar nada
        }
        catch
        {
            evento.Concluir("ErroTecnico", null);
            await db.SaveChangesAsync(ct);
            return;
        }

        if (resultado.Resultado == ResultadoTransmissao.Autorizado)
        {
            evento.Concluir("Concluido", resultado.Protocolo);
            doc.RegistrarRetorno(resultado.CStat ?? "", resultado.Motivo, resultado.Protocolo);
            doc.TransicionarPara(StatusDocumentoFiscal.Cancelado);
            await db.SaveChangesAsync(ct);
            await arquivador.ArquivarEventoAsync(doc, evento.Id.Value.ToString(), evento.Justificativa ?? "", ct);
            await audit.LogAsync(doc.TenantId, null, "fiscal.documento.cancelado", "DocumentoFiscal",
                doc.Id.Value.ToString(), ct);
        }
        else if (resultado.Resultado == ResultadoTransmissao.Rejeitado)
        {
            evento.Concluir("Rejeitado", resultado.Protocolo);
            await db.SaveChangesAsync(ct);
        }
        // Desconhecido/ErroTecnico: mantém pendente para a próxima varredura.
    }

    private void AplicarConsulta(DocumentoFiscal doc, TransmissaoResult consulta)
    {
        if (consulta.Resultado == ResultadoTransmissao.Autorizado)
        {
            doc.RegistrarRetorno(consulta.CStat ?? "", consulta.Motivo, consulta.Protocolo);
            doc.TransicionarPara(StatusDocumentoFiscal.Autorizado);
        }
        else if (consulta.Resultado == ResultadoTransmissao.Rejeitado)
        {
            doc.RegistrarRetorno(consulta.CStat ?? "", consulta.Motivo, consulta.Protocolo);
            doc.TransicionarPara(StatusDocumentoFiscal.Rejeitado);
        }
        else if (consulta.Resultado == ResultadoTransmissao.Aguardando && consulta.Protocolo is not null)
        {
            // Recibo preservado para o próximo polling (nunca reenvio cego).
            doc.RegistrarRetorno(consulta.CStat ?? "", consulta.Motivo, consulta.Protocolo);
        }
    }

    /// <summary>Recibo SEFAZ: 15 dígitos numéricos.</summary>
    internal static bool EhRecibo(string? protocolo)
        => protocolo is not null && protocolo.Length == 15 && protocolo.All(char.IsDigit);

    internal static DocumentoContexto ContextoDe(DocumentoFiscal doc) => new(
        doc.Id.Value, doc.Tipo, doc.Ambiente, doc.Serie, doc.Numero, doc.ChaveAcesso,
        doc.SnapshotEmitente, doc.SnapshotDestinatario, doc.SnapshotItens, doc.SnapshotTotais);
}
