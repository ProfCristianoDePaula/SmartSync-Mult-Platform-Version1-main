using System.Security.Claims;
using FluentValidation;
using Identity.Application.Branches;
using Identity.Application.Common;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// CRUD de filiais, sempre aninhado em um tenant (Etapa 14). Exclusivo de
/// SuperAdmin (qualquer tenant) e TenantAdmin (apenas o próprio tenant).
///
/// Autorização por claim: o TenantId do usuário vem do JWT
/// (<see cref="JwtClaims.TenantId"/>) e NUNCA confiamos apenas no
/// <c>{tenantId}</c> da rota — usuários com a claim precisam que os dois
/// batam, senão 403. SuperAdmin global (sem a claim) opera em qualquer tenant.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId:guid}/branches")]
[Authorize(Roles = Roles.SuperAdmin + "," + Roles.TenantAdmin)]
public sealed class BranchesController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchesController(IBranchService branchService) => _branchService = branchService;

    /// <summary>Cadastra uma filial no tenant informado na rota.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromRoute] Guid tenantId,
        [FromBody] CreateBranchCommand command,
        CancellationToken ct)
    {
        if (!IsTenantAccessible(tenantId))
            return ForbiddenTenant();

        try
        {
            var branch = await _branchService.CreateAsync(command with { TenantId = tenantId }, ct);
            if (branch is null)
                return NotFound(new { title = "Tenant não encontrado." });

            return CreatedAtAction(
                nameof(GetById),
                new { tenantId, branchId = branch.Id },
                branch);
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

    /// <summary>Edita uma filial existente (ids da rota prevalecem sobre o body).</summary>
    [HttpPut("{branchId:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid branchId,
        [FromBody] UpdateBranchCommand command,
        CancellationToken ct)
    {
        if (!IsTenantAccessible(tenantId))
            return ForbiddenTenant();

        try
        {
            var branch = await _branchService.UpdateAsync(
                command with { TenantId = tenantId, BranchId = branchId }, ct);
            if (branch is null)
                return NotFound(new { title = "Filial não encontrada." });

            return Ok(branch);
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

    /// <summary>Soft delete: inativa a filial (não remove fisicamente).</summary>
    [HttpDelete("{branchId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoftDelete(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid branchId,
        CancellationToken ct)
    {
        if (!IsTenantAccessible(tenantId))
            return ForbiddenTenant();

        var deleted = await _branchService.SoftDeleteAsync(
            new SoftDeleteBranchCommand(tenantId, branchId), ct);
        if (!deleted)
            return NotFound(new { title = "Filial não encontrada." });

        return NoContent();
    }

    /// <summary>Lista paginada das filiais DESTE tenant (nunca de outro).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromRoute] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (!IsTenantAccessible(tenantId))
            return ForbiddenTenant();

        return Ok(await _branchService.ListByTenantAsync(
            new ListBranchesByTenantQuery(tenantId, page, pageSize, includeInactive), ct));
    }

    /// <summary>Detalhe de uma filial do tenant (apenas ativas).</summary>
    [HttpGet("{branchId:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid branchId,
        CancellationToken ct)
    {
        if (!IsTenantAccessible(tenantId))
            return ForbiddenTenant();

        var branch = await _branchService.GetByIdAsync(
            new GetBranchByIdQuery(tenantId, branchId), ct);
        if (branch is null)
            return NotFound(new { title = "Filial não encontrada." });

        return Ok(branch);
    }

    /// <summary>
    /// Valida que o <c>{tenantId}</c> da rota é acessível para o usuário
    /// autenticado. A claim <c>tenant_id</c> do JWT é a fonte da verdade: quem a
    /// tem só acessa o próprio tenant; quem não tem (SuperAdmin global) acessa
    /// qualquer um.
    /// </summary>
    private bool IsTenantAccessible(Guid routeTenantId)
    {
        var claimTenantId = User.FindFirstValue(JwtClaims.TenantId);
        if (claimTenantId is null)
            return true;

        return Guid.TryParse(claimTenantId, out var tenantId)
               && tenantId == routeTenantId;
    }

    private ObjectResult ForbiddenTenant()
        => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Acesso negado a este tenant.",
            Status = StatusCodes.Status403Forbidden
        });

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
