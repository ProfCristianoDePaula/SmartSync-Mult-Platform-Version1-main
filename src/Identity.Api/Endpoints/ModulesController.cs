using FluentValidation;
using Identity.Application.Common;
using Identity.Application.Modules;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// CRUD do catálogo de módulos (Etapa 15). Exclusivo da role SuperAdmin — o
/// Slug é o identificador estável que os demais microsserviços usam para saber
/// se o tenant contratou o módulo, por isso é imutável após a criação.
/// </summary>
[ApiController]
[Route("api/modules")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class ModulesController : ControllerBase
{
    private readonly IModuleService _moduleService;

    public ModulesController(IModuleService moduleService) => _moduleService = moduleService;

    /// <summary>Cadastra um novo módulo.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ModuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateModuleCommand command,
        CancellationToken ct)
    {
        try
        {
            var module = await _moduleService.CreateAsync(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = module.Id }, module);
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

    /// <summary>Edita um módulo existente (o Id da rota prevalece sobre o body;
    /// o Slug é imutável).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateModuleCommand command,
        CancellationToken ct)
    {
        try
        {
            var module = await _moduleService.UpdateAsync(command with { Id = id }, ct);
            if (module is null)
                return NotFound(new { title = "Módulo não encontrado." });

            return Ok(module);
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

    /// <summary>Soft delete: inativa o módulo (não remove fisicamente).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete([FromRoute] Guid id, CancellationToken ct)
    {
        var deleted = await _moduleService.SoftDeleteAsync(new SoftDeleteModuleCommand(id), ct);
        if (!deleted)
            return NotFound(new { title = "Módulo não encontrado." });

        return NoContent();
    }

    /// <summary>
    /// Lista módulos paginada. Por padrão exclui inativos; use
    /// <c>includeInactive=true</c> para incluí-los.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
        => Ok(await _moduleService.ListAsync(new ListModulesQuery(page, pageSize, includeInactive), ct));

    /// <summary>Detalhe de um módulo (apenas ativos).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var module = await _moduleService.GetByIdAsync(new GetModuleByIdQuery(id), ct);
        if (module is null)
            return NotFound(new { title = "Módulo não encontrado." });

        return Ok(module);
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
