using Fiscal.Api.Extensions;
using Fiscal.Application.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Sonda operacional do Fiscal (Fiscal-1): prova o gate de módulo `fiscal`
/// e exercita o `IAuditLogger` de ponta a ponta. Sem regra fiscal aqui —
/// emissão real começa na Fiscal-8.
/// </summary>
[ApiController]
[Route("api/fiscal")]
[Authorize(Policy = "module-fiscal")]
public sealed class FiscalController : FiscalControllerBase
{
    private readonly IAuditLogger _auditLogger;

    public FiscalController(IAuditLogger auditLogger) => _auditLogger = auditLogger;

    [HttpGet("ping")]
    [ProducesResponseType(typeof(PingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        var tenantId = User.GetRequiredTenantId();
        var userId = User.GetUserId();

        await _auditLogger.LogAsync(tenantId, userId, "fiscal.ping", "FiscalPing", null, ct);

        return Ok(new PingResponse("ok", tenantId.Value, DateTime.UtcNow));
    }

    public sealed record PingResponse(string Status, Guid TenantId, DateTime AgoraUtc);
}
