using FluentValidation;
using Identity.Application.Common;
using Identity.Application.TenantModules;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// Vínculos tenant↔módulo (Etapa 15). Exclusivo da role SuperAdmin (decisão do
/// usuário: contratação/billing é sensível; TenantAdmin ainda não opera
/// vínculos nem no próprio tenant). As rotas são aninhadas em
/// <c>api/tenants/{tenantId}/modules</c> e o TenantId é sempre imposto nas
/// queries — um vínculo nunca atravessa tenants.
///
/// Semântica: POST vincula (400 se já existir vínculo ativo); PUT troca o
/// plano (inativa a vigência atual e abre uma nova — mesmo plano é idempotente;
/// sem vínculo ativo, reativa); DELETE encerra a vigência ativa (404 se não
/// houver); GET lista (por padrão só os ativos; includeInactive traz histórico).
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId:guid}/modules")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class TenantModulesController : ControllerBase
{
    private readonly ITenantModuleService _tenantModuleService;

    public TenantModulesController(ITenantModuleService tenantModuleService)
        => _tenantModuleService = tenantModuleService;

    /// <summary>Vincula o tenant a um módulo com o plano informado.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantModuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Link(
        [FromRoute] Guid tenantId,
        [FromBody] LinkTenantModuleCommand command,
        CancellationToken ct)
    {
        try
        {
            var link = await _tenantModuleService.LinkAsync(command with { TenantId = tenantId }, ct);
            if (link is null)
                return NotFound(new { title = "Tenant não encontrado." });

            return CreatedAtAction(
                nameof(List),
                new { tenantId },
                link);
        }
        catch (ValidationException ex)
        {
            return ValidationFailure(ex);
        }
        catch (BusinessRuleViolationException ex)
        {
            return BusinessRuleFailure(ex);
        }
    }

    /// <summary>
    /// Troca o plano do vínculo ativo (upgrade/downgrade). Mesmo plano é
    /// idempotente; sem vínculo ativo, cria um novo (reativação).
    /// </summary>
    [HttpPut("{moduleId:guid}")]
    [ProducesResponseType(typeof(TenantModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid moduleId,
        [FromBody] UpdateTenantModuleCommand command,
        CancellationToken ct)
    {
        try
        {
            var link = await _tenantModuleService.UpdateAsync(
                command with { TenantId = tenantId, ModuleId = moduleId }, ct);
            if (link is null)
                return NotFound(new { title = "Tenant não encontrado." });

            return Ok(link);
        }
        catch (ValidationException ex)
        {
            return ValidationFailure(ex);
        }
        catch (BusinessRuleViolationException ex)
        {
            return BusinessRuleFailure(ex);
        }
    }

    /// <summary>Encerra o vínculo ativo do tenant com o módulo (preserva histórico).</summary>
    [HttpDelete("{moduleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlink(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid moduleId,
        CancellationToken ct)
    {
        var unlinked = await _tenantModuleService.UnlinkAsync(
            new UnlinkTenantModuleCommand(tenantId, moduleId), ct);
        if (!unlinked)
            return NotFound(new { title = "Vínculo ativo não encontrado." });

        return NoContent();
    }

    /// <summary>
    /// Lista paginada dos vínculos DESTE tenant (nunca de outro). Por padrão
    /// apenas os ATIVOS; use <c>includeInactive=true</c> para incluir o
    /// histórico (vigências encerradas).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TenantModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromRoute] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await _tenantModuleService.ListAsync(
            new ListTenantModulesQuery(tenantId, page, pageSize, includeInactive), ct));

    private BadRequestObjectResult ValidationFailure(ValidationException ex)
        => BadRequest(new ProblemDetails
        {
            Title = "Dados inválidos.",
            Detail = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)),
            Status = StatusCodes.Status400BadRequest
        });

    private BadRequestObjectResult BusinessRuleFailure(BusinessRuleViolationException ex)
        => BadRequest(new ProblemDetails
        {
            Title = "Regra de negócio violada.",
            Detail = ex.Message,
            Status = StatusCodes.Status400BadRequest
        });
}
