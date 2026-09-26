using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fiscal.Application.Auditing;
using Fiscal.Application.Cadastros;
using Fiscal.Application.Documentos;
using Fiscal.Application.Emissao;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;
using Fiscal.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Emissao;

public sealed class EmissaoService(
    IDocumentoFiscalRepository documentos,
    IEmitenteFiscalRepository emitentes,
    IUfFiscalRepository ufs,
    ISerieNumeracaoService numeracao,
    IValidadorFiscal validador,
    IConcessaoRepository concessoes,
    FiscalDbContext db,
    IAuditLogger audit,
    IConciliacaoService conciliacao,
    IValidator<CriarDocumentoRequest> createValidator,
    IValidator<CancelarDocumentoRequest> cancelValidator) : IEmissaoService
{
    public async Task<CriacaoDocumentoResult> CriarAsync(
        CriarDocumentoRequest request, string idempotencyKey, Guid userId, string role, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
            throw new BusinessRuleViolationException("Header Idempotency-Key obrigatório (≤200).");

        var chave = idempotencyKey.Trim();

        var hash = HashRequisicao(request);
        var existente = await documentos.GetByIdempotencyAsync(request.TenantId, chave, ct);
        if (existente is not null)
        {
            if (!string.Equals(existente.RequestHash, hash, StringComparison.Ordinal))
                throw new BusinessRuleViolationException(
                    "Mesma chave de idempotência com conteúdo diferente.", "fiscal.idempotencia-conflito");
            return new CriacaoDocumentoResult(ToDto(existente), false);
        }

        var emitente = await emitentes.GetAsync(request.TenantId, request.EmitenteId, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");

        await ExigirEmissaoAsync(request.TenantId, emitente.BranchId, userId, role, ct);

        // Série da configuração de homologação (ou "1"); ambiente inicial sempre homologação.
        var serie = "1";
        var cfg = await db.ConfiguracoesDocumento.FirstOrDefaultAsync(
            c => c.EmitenteId == emitente.Id && c.Tipo == request.Tipo && c.Ambiente == AmbienteFiscal.Homologacao, ct);
        if (cfg is not null && !string.IsNullOrWhiteSpace(cfg.Serie)) serie = cfg.Serie;

        var modeloInt = request.Tipo == TipoDocumentoFiscal.NFSe ? 200 : (int)request.Tipo;
        var numero = await numeracao.ReservarAsync(emitente.Id, modeloInt, serie, AmbienteFiscal.Homologacao, ct);

        string? chaveAcesso = null;
        if (request.Tipo is TipoDocumentoFiscal.NFe55 or TipoDocumentoFiscal.NFCe65)
        {
            var uf = await ufs.GetBySiglaAsync(emitente.Endereco.State, ct);
            var agora = DateTime.UtcNow;
            chaveAcesso = ChaveAcesso.Montar(
                uf?.CodigoIbge ?? throw new BusinessRuleViolationException("UF do emitente fora do catálogo."),
                agora.Year, agora.Month, emitente.Cnpj.Numero, (int)request.Tipo,
                int.TryParse(serie, out var s) ? s : 1, numero, 1,
                RandomNumberGenerator.GetInt32(0, 100000000)).Numero;
        }

        var snapshotEmitente = JsonSerializer.Serialize(new
        {
            emitente.Cnpj.Numero,
            emitente.RazaoSocial,
            emitente.Fantasia,
            emitente.Endereco.City,
            emitente.Endereco.State,
            emitente.Endereco.CodigoIbgeMunicipio,
            Crt = (int)emitente.Crt
        });
        var totais = request.SnapshotTotais;
        if (!string.IsNullOrWhiteSpace(request.ObservacaoPedido))
            totais = MesclarObservacao(totais, request.ObservacaoPedido.Trim());

        var doc = DocumentoFiscal.Criar(request.TenantId, emitente.Id, request.Tipo,
            AmbienteFiscal.Homologacao, serie, numero, chaveAcesso,
            request.OrigemVendaId, request.OrigemPedidoId, chave, hash,
            snapshotEmitente, request.SnapshotDestinatario, request.SnapshotItens, totais);

        // Persiste a intenção ANTES de qualquer chamada externa (R6/R7),
        // percorrendo a máquina: validação local OK → assinado (Fiscal-9
        // assina o XML real entre Assinado e EnvioPendente) → enfileirado.
        doc.TransicionarPara(StatusDocumentoFiscal.Validando);
        doc.TransicionarPara(StatusDocumentoFiscal.Assinado);
        doc.TransicionarPara(StatusDocumentoFiscal.EnvioPendente);
        await documentos.AddAsync(doc, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corrida de idempotência (R7): a outra tentativa venceu.
            db.ChangeTracker.Clear();
            var vencedor = await documentos.GetByIdempotencyAsync(request.TenantId, chave, ct);
            if (vencedor is not null && string.Equals(vencedor.RequestHash, hash, StringComparison.Ordinal))
                return new CriacaoDocumentoResult(ToDto(vencedor), false);
            throw new BusinessRuleViolationException(
                "Mesma chave de idempotência com conteúdo diferente.", "fiscal.idempotencia-conflito");
        }

        await audit.LogAsync(request.TenantId, userId, "fiscal.documento.criado", "DocumentoFiscal",
            doc.Id.Value.ToString(), ct);

        return new CriacaoDocumentoResult(ToDto(doc), true);
    }

    public async Task<DocumentoDto?> GetByIdAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
    {
        var doc = await documentos.GetAsync(tenantId, id, ct);
        return doc is null ? null : ToDto(doc);
    }

    public async Task<IReadOnlyList<DocumentoDto>> ListAsync(
        TenantId tenantId, StatusDocumentoFiscal? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.DocumentosFiscais.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (status is not null)
            query = query.Where(d => d.Status == status);
        var items = await query.OrderByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<EventoDto> SolicitarCancelamentoAsync(
        TenantId tenantId, Guid id, CancelarDocumentoRequest request, Guid userId, string role, CancellationToken ct = default)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);

        var doc = await documentos.GetAsync(tenantId, id, ct)
            ?? throw new BusinessRuleViolationException("Documento não encontrado.");
        if (doc.Status != StatusDocumentoFiscal.Autorizado)
            throw new BusinessRuleViolationException("Somente documento autorizado pode ser cancelado.");

        var emitenteCanc = await emitentes.GetAsync(tenantId, doc.EmitenteId.Value, ct)
            ?? throw new BusinessRuleViolationException("Emitente não encontrado.");
        await ExigirEmissaoAsync(tenantId, emitenteCanc.BranchId, userId, role, ct);

        var evento = EventoFiscal.Criar(tenantId, doc.Id, TipoEventoFiscal.Cancelamento, null, request.Justificativa);
        db.EventosFiscais.Add(evento);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(tenantId, userId, "fiscal.cancelamento.solicitado", "EventoFiscal",
            evento.Id.Value.ToString(), ct);

        return new EventoDto(evento.Id.Value, doc.Id.Value, evento.Tipo, evento.CodigoEvento, evento.Status, evento.Protocolo);
    }

    public async Task<DocumentoDto> ConciliarAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
    {
        await conciliacao.ConciliarDocumentoAsync(id, ct);
        var doc = await documentos.GetAsync(tenantId, id, ct)
            ?? throw new BusinessRuleViolationException("Documento não encontrado.");
        return ToDto(doc);
    }

    public async Task<string?> ObterXmlAsync(TenantId tenantId, Guid id, CancellationToken ct = default)
    {
        var doc = await documentos.GetAsync(tenantId, id, ct);
        return doc?.XmlUltimoEnvio;
    }

    public async Task<IReadOnlyList<DocumentoDto>> ListarPorVendaAsync(TenantId tenantId, Guid vendaId, CancellationToken ct = default)
    {
        var items = await documentos.ListByVendaAsync(tenantId, vendaId, ct);
        return items.Select(ToDto).ToList();
    }

    private async Task ExigirEmissaoAsync(
        TenantId tenantId, BranchId branchId, Guid userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, PlatformRoles.TenantAdmin, StringComparison.Ordinal)
            || string.Equals(role, PlatformRoles.SuperAdmin, StringComparison.Ordinal))
            return;

        if ((string.Equals(role, PlatformRoles.Manager, StringComparison.Ordinal)
             || string.Equals(role, PlatformRoles.Seller, StringComparison.Ordinal))
            && await concessoes.FindAsync(tenantId, branchId, userId, PapelUnidade.Emitir, ct) is not null)
            return;

        throw new UnauthorizedAccessException("Sem concessão de emissão nesta unidade.");
    }

    /// <summary>Injeta a observação como propriedade JSON (nunca concatena texto).</summary>
    internal static string MesclarObservacao(string totaisJson, string observacao)
    {
        try
        {
            using var doc = JsonDocument.Parse(totaisJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return totaisJson;
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    prop.WriteTo(writer);
                writer.WriteString("obsPedido", observacao);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return totaisJson;
        }
    }

    internal static string HashRequisicao(CriarDocumentoRequest request)
    {
        var canonico = JsonSerializer.Serialize(new
        {
            request.EmitenteId, Tipo = (int)request.Tipo, request.Serie,
            request.OrigemVendaId, request.OrigemPedidoId,
            request.SnapshotDestinatario, request.SnapshotItens, request.SnapshotTotais,
            request.ObservacaoPedido
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    internal static DocumentoDto ToDto(DocumentoFiscal d) => new(
        d.Id.Value, d.EmitenteId.Value, d.Tipo, d.Ambiente, d.Status,
        d.Serie, d.Numero, d.ChaveAcesso, d.Protocolo, d.CStat, d.Motivo,
        d.OrigemVendaId, d.Tentativas, d.CreatedAtUtc, d.UpdatedAtUtc);
}
