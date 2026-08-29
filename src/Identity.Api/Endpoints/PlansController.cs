using FluentValidation;
using Identity.Application.Common;
using Identity.Application.Plans;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// CRUD do catálogo de planos de assinatura, aninhado em um módulo (Etapa 15).
/// Exclusivo da role SuperAdmin — um plano inativo (soft delete) não pode mais
/// ser vinculado a Tenants. O <c>{moduleId}</c> da rota é sempre imposto nas
/// queries (a listagem nunca atravessa módulos).
/// </summary>
[ApiController]
[Route("api/modules/{moduleId:guid}/plans")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class PlansController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlansController(IPlanService planService) => _planService = planService;

    /// <summary>Cadastra um novo plano dentro do módulo informado na rota.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromRoute] Guid moduleId,
        [FromBody] CreatePlanCommand command,
        CancellationToken ct)
    {
        try
        {
            var plan = await _planService.CreateAsync(command with { ModuleId = moduleId }, ct);
            if (plan is null)
                return NotFound(new { title = "Módulo não encontrado." });

            return CreatedAtAction(
                nameof(GetById),
                new { moduleId, id = plan.Id },
                plan);
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

    /// <summary>Edita um plano existente (ids da rota prevalecem sobre o body).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid moduleId,
        [FromRoute] Guid id,
        [FromBody] UpdatePlanCommand command,
        CancellationToken ct)
    {
        try
        {
            var plan = await _planService.UpdateAsync(command with { ModuleId = moduleId, Id = id }, ct);
            if (plan is null)
                return NotFound(new { title = "Plano não encontrado." });

            return Ok(plan);
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

    /// <summary>Soft delete: inativa o plano (não remove fisicamente).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(
        [FromRoute] Guid moduleId,
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var deleted = await _planService.SoftDeleteAsync(new SoftDeletePlanCommand(moduleId, id), ct);
        if (!deleted)
            return NotFound(new { title = "Plano não encontrado." });

        return NoContent();
    }

    /// <summary>
    /// Lista paginada dos planos DESTE módulo (nunca de outro). Por padrão
    /// exclui inativos; use <c>includeInactive=true</c> para incluí-los.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromRoute] Guid moduleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await _planService.ListAsync(new ListPlansQuery(moduleId, page, pageSize, includeInactive), ct));

    /// <summary>Detalhe de um plano do módulo (apenas ativos).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid moduleId,
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var plan = await _planService.GetByIdAsync(new GetPlanByIdQuery(moduleId, id), ct);
        if (plan is null)
            return NotFound(new { title = "Plano não encontrado." });

        return Ok(plan);
    }

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
