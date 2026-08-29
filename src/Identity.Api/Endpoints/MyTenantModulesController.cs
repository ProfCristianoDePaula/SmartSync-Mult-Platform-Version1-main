using System.Security.Claims;
using Identity.Application.TenantModules;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// Consulta dos módulos do tenant do PRÓPRIO token (contrato recomendado §12.2
/// do CONTRATO-IDENTIDADE). Qualquer role autenticada COM a claim
/// <c>tenant_id</c> consulta os módulos ativos contratados pelo SEU tenant — é o
/// endpoint que os demais microsserviços usam para validar acesso/limites em
/// runtime, sem depender de token de SuperAdmin.
///
/// O TenantId vem da claim <see cref="JwtClaims.TenantId"/> e NUNCA de query
/// param — assim não há risco de vazar o catálogo de outro tenant. Usuário sem a
/// claim (ex.: SuperAdmin global) recebe 403.
/// </summary>
[ApiController]
[Route("api/tenants/me/modules")]
[Authorize]
public sealed class MyTenantModulesController : ControllerBase
{
    private readonly ITenantModuleService _tenantModuleService;

    public MyTenantModulesController(ITenantModuleService tenantModuleService)
        => _tenantModuleService = tenantModuleService;

    /// <summary>Lista os vínculos ativos do tenant do token.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantModulesView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var claimTenantId = User.FindFirstValue(JwtClaims.TenantId);
        if (claimTenantId is null || !Guid.TryParse(claimTenantId, out var tenantId))
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Usuário sem tenant vinculado.",
                Status = StatusCodes.Status403Forbidden
            });

        var items = await _tenantModuleService.ListActiveForTenantAsync(tenantId, ct);
        return Ok(new TenantModulesView(items));
    }
}
