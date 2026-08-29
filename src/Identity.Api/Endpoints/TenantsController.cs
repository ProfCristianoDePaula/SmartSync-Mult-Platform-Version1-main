using FluentValidation;
using Identity.Application.Common;
using Identity.Application.Tenants;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// CRUD de tenants. Exclusivo da role SuperAdmin — um tenant inativado (soft
/// delete) fica fora das consultas e seus usuários não podem mais autenticar.
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService) => _tenantService = tenantService;

    /// <summary>Cadastra um novo tenant (o plano é opcional no nascimento).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantCommand command,
        CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.CreateAsync(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
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

    /// <summary>Edita um tenant existente (o Id da rota prevalece sobre o body).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateTenantCommand command,
        CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.UpdateAsync(command with { Id = id }, ct);
            if (tenant is null)
                return NotFound(new { title = "Tenant não encontrado." });

            return Ok(tenant);
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

    /// <summary>Soft delete: inativa o tenant (não remove fisicamente).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete([FromRoute] Guid id, CancellationToken ct)
    {
        var deleted = await _tenantService.SoftDeleteAsync(new SoftDeleteTenantCommand(id), ct);
        if (!deleted)
            return NotFound(new { title = "Tenant não encontrado." });

        return NoContent();
    }

    /// <summary>
    /// Lista tenants paginada, com filtros por status, busca por nome e documento
    /// (CPF/CNPJ). Por padrão exclui soft-deletados; use <c>includeInactive=true</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? documento = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var statusFilter = Enum.TryParse<Domain.Enums.TenantStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : (Domain.Enums.TenantStatus?)null;

        return Ok(await _tenantService.ListAsync(
            new ListTenantsQuery(page, pageSize, statusFilter, search, documento, includeInactive), ct));
    }

    /// <summary>Detalhe de um tenant (apenas ativos).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var tenant = await _tenantService.GetByIdAsync(new GetTenantByIdQuery(id), ct);
        if (tenant is null)
            return NotFound(new { title = "Tenant não encontrado." });

        return Ok(tenant);
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
