using Fiscal.Api.Extensions;
using Fiscal.Application.Certificados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>Alertas operacionais do Fiscal (ex.: certificado expirando).</summary>
[ApiController]
[Route("api/fiscal/alertas")]
[Authorize(Policy = "tenant")]
public sealed class AlertasController : FiscalControllerBase
{
    private readonly IAlertaService _service;

    public AlertasController(IAlertaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool apenasNaoLidas = true, CancellationToken ct = default)
        => Ok(await _service.ListAsync(User.GetRequiredTenantId(), apenasNaoLidas, ct));

    [HttpPatch("{id:guid}/lida")]
    public async Task<IActionResult> MarcarLida(Guid id, CancellationToken ct)
    {
        var ok = await _service.MarcarLidaAsync(User.GetRequiredTenantId(), id, ct);
        return ok ? NoContent() : NotFound("Alerta não encontrado.");
    }
}
