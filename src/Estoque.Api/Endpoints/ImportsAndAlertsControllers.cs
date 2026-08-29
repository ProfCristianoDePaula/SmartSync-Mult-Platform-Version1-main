using Estoque.Api.Extensions;
using Estoque.Application.Common;
using Estoque.Application.Integracao;
using Estoque.Application.Politicas;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>Importação de XML NF-e (multipart) com status por polling.</summary>
[ApiController]
[Route("api/xml-imports")]
[Authorize(Policy = "tenant")]
public sealed class XmlImportsController : EstoqueControllerBase
{
    private readonly IXmlImportService _imports;

    public XmlImportsController(IXmlImportService imports) => _imports = imports;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(XmlImportDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Upload(
        IFormFile file, [FromForm] Guid branchId, [FromForm] Guid? supplierId, CancellationToken ct)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new ProblemDetails
                { Title = "Arquivo XML é obrigatório.", Status = 400 });

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            var import = await _imports.EnqueueAsync(
                User.GetRequiredTenantId(), branchId, supplierId,
                file.FileName, ms.ToArray(), User.GetUserId(), ct);

            return Accepted(import);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{importId:guid}")]
    [ProducesResponseType(typeof(XmlImportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(Guid importId, CancellationToken ct)
    {
        var import = await _imports.GetByIdAsync(User.GetRequiredTenantId(), importId, ct);
        return import is null ? NotFound("Importação não encontrada.") : Ok(import);
    }
}

/// <summary>Alertas gerados pelos jobs/políticas — leitura geral; ack Manager+.</summary>
[ApiController]
[Route("api/alerts")]
[Authorize(Policy = "tenant")]
public sealed class AlertsController : EstoqueControllerBase
{
    private readonly IAlertService _alerts;

    public AlertsController(IAlertService alerts) => _alerts = alerts;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AlertDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool unacknowledgedOnly = true, [FromQuery] AlertType? type = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var query = new ListAlertsQuery(unacknowledgedOnly, type, page, pageSize)
                .WithTenant(User.GetRequiredTenantId());
            return Ok(await _alerts.ListAsync(query, ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }

    [HttpPatch("{alertId:guid}/acknowledge")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Acknowledge(Guid alertId, CancellationToken ct)
    {
        var acked = await _alerts.AcknowledgeAsync(
            new AcknowledgeAlertCommand(alertId).WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
        return acked ? NoContent() : NotFound("Alerta não encontrado ou já reconhecido.");
    }
}

