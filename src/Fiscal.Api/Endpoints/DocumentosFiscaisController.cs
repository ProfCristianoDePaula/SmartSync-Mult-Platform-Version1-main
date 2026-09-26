using Fiscal.Api.Extensions;
using Fiscal.Application.Documentos;
using Fiscal.Application.Emissao;
using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Pipeline de emissão (Fiscal-8, simulador): 202 + Location após persistir
/// trabalho durável; idempotência por header; conciliação manual; downloads
/// autorizados por tenant. Emissão exige concessão Emitir (Manager/Seller) ou
/// TenantAdmin.
/// </summary>
[ApiController]
[Route("api/fiscal/documentos")]
[Authorize(Policy = "tenant")]
public sealed class DocumentosFiscaisController : FiscalControllerBase
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private readonly IEmissaoService _emissao;

    public DocumentosFiscaisController(IEmissaoService emissao) => _emissao = emissao;

    [HttpPost]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Criar(
        [FromBody] CriarDocumentoRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return BusinessRuleFailure(new BusinessRuleViolationException("Header Idempotency-Key obrigatório (≤200)."));

            var resultado = await _emissao.CriarAsync(
                request.WithTenant(User.GetRequiredTenantId()),
                idempotencyKey, User.GetUserId(), UserRole(), ct);
            return resultado.CriadoNovo
                ? AcceptedAtAction(nameof(GetById), new { id = resultado.Documento.Id }, resultado.Documento)
                : Ok(resultado.Documento);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex)
        {
            return ex.Code == "fiscal.idempotencia-conflito"
                ? Conflict(new ProblemDetails
                {
                    Title = "Conflito de idempotência.",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict
                })
                : BusinessRuleFailure(ex);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _emissao.GetByIdAsync(User.GetRequiredTenantId(), id, ct);
        return dto is null ? NotFound("Documento não encontrado.") : Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        StatusDocumentoFiscal? st = status is null ? null : (StatusDocumentoFiscal)status.Value;
        return Ok(await _emissao.ListAsync(User.GetRequiredTenantId(), st, page, pageSize, ct));
    }

    /// <summary>Status fiscal da venda (fonte da verdade: documentos vinculados).</summary>
    [HttpGet("por-venda/{vendaId:guid}")]
    public async Task<IActionResult> PorVenda(Guid vendaId, CancellationToken ct)
        => Ok(await _emissao.ListarPorVendaAsync(User.GetRequiredTenantId(), vendaId, ct));

    [HttpPost("{id:guid}/cancelamento")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    [ProducesResponseType(typeof(EventoDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Cancelar(
        Guid id, [FromBody] CancelarDocumentoRequest request, CancellationToken ct)
    {
        try
        {
            var evento = await _emissao.SolicitarCancelamentoAsync(
                User.GetRequiredTenantId(), id, request, User.GetUserId(), UserRole(), ct);
            return AcceptedAtAction(nameof(GetById), new { id }, evento);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPost("{id:guid}/consultar")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    public async Task<IActionResult> Consultar(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _emissao.ConciliarAsync(User.GetRequiredTenantId(), id, ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{id:guid}/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> Xml(Guid id, CancellationToken ct)
    {
        var xml = await _emissao.ObterXmlAsync(User.GetRequiredTenantId(), id, ct);
        return xml is null ? NotFound("XML ainda indisponível.") : Content(xml, "application/xml");
    }

    [HttpGet("{id:guid}/arquivos")]
    public async Task<IActionResult> Arquivos(
        Guid id,
        [FromServices] Fiscal.Application.Arquivos.IArmazenamentoFiscal arquivos,
        [FromServices] Fiscal.Application.Documentos.IDocumentoFiscalRepository documentos,
        CancellationToken ct)
    {
        var tenantId = User.GetRequiredTenantId();
        var doc = await documentos.GetAsync(tenantId, id, ct);
        if (doc is null) return NotFound("Documento não encontrado.");
        var pasta = string.IsNullOrWhiteSpace(doc.ChaveAcesso) ? doc.Id.Value.ToString("N") : doc.ChaveAcesso;
        return Ok(await arquivos.ListarAsync(tenantId, pasta, ct));
    }

    [HttpGet("{id:guid}/arquivos/{nome}")]
    public async Task<IActionResult> Arquivo(
        Guid id,
        string nome,
        [FromServices] Fiscal.Application.Arquivos.IArmazenamentoFiscal arquivos,
        [FromServices] Fiscal.Application.Documentos.IDocumentoFiscalRepository documentos,
        CancellationToken ct)
    {
        try
        {
            var tenantId = User.GetRequiredTenantId();
            var doc = await documentos.GetAsync(tenantId, id, ct);
            if (doc is null) return NotFound("Documento não encontrado.");
            var pasta = string.IsNullOrWhiteSpace(doc.ChaveAcesso) ? doc.Id.Value.ToString("N") : doc.ChaveAcesso;
            var arquivo = await arquivos.ObterAsync(tenantId, pasta, nome, ct);
            return arquivo is null ? NotFound("Arquivo não encontrado.") : File(arquivo.Conteudo, arquivo.ContentType, nome);
        }
        catch (Fiscal.Domain.Common.BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{id:guid}/danfe")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Danfe(
        Guid id,
        [FromServices] Fiscal.Application.Arquivos.IDanfeGerador danfe,
        [FromServices] Fiscal.Application.Documentos.IDocumentoFiscalRepository documentos,
        CancellationToken ct)
    {
        try
        {
            var doc = await documentos.GetAsync(User.GetRequiredTenantId(), id, ct);
            if (doc is null) return NotFound("Documento não encontrado.");
            var pdf = danfe.Gerar(doc);
            return File(pdf, "application/pdf", $"danfe-{id:N}.pdf");
        }
        catch (Fiscal.Domain.Common.BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    private string UserRole()
        => User.FindFirst(Fiscal.Domain.Common.JwtClaims.Role)?.Value ?? "";
}
