using Estoque.Api.Extensions;
using Estoque.Application.Common;
using Estoque.Application.Movimentacoes;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>
/// Operações de estoque por FILIAL: entrada, saída, ajuste (inventário) e
/// transferência entre filiais do mesmo tenant. Filial chega no BODY e é
/// validada contra o tenant do token (Estratégia de FilialId, Etapa 23).
/// Roles: Manager/Seller operam; ajuste e transferência exigem Manager+.
/// </summary>
[ApiController]
[Route("api/stock")]
[Authorize(Policy = "tenant")]
public sealed class StockController : EstoqueControllerBase
{
    private readonly IStockMovementService _movements;

    public StockController(IStockMovementService movements) => _movements = movements;

    /// <summary>Entrada de estoque (compra/recebimento), com lote opcional.</summary>
    [HttpPost("in")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager + "," + PlatformRoles.Seller)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> StockIn([FromBody] RegisterStockInCommand command, CancellationToken ct)
    {
        try
        {
            var movement = await _movements.RegisterInAsync(
                command.WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
            return CreatedAtAction(nameof(ListMovements), new { }, movement);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    /// <summary>Saída de estoque (venda/consumo/baixa).</summary>
    [HttpPost("out")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager + "," + PlatformRoles.Seller)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> StockOut([FromBody] RegisterStockOutCommand command, CancellationToken ct)
    {
        try
        {
            var movement = await _movements.RegisterOutAsync(
                command.WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
            return CreatedAtAction(nameof(ListMovements), new { }, movement);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    /// <summary>Ajuste de inventário (delta ±) — exige motivo para auditoria.</summary>
    [HttpPost("adjustments")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(StockMovementDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Adjust([FromBody] AdjustStockCommand command, CancellationToken ct)
    {
        try
        {
            var movement = await _movements.AdjustAsync(
                command.WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
            return CreatedAtAction(nameof(ListMovements), new { }, movement);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    /// <summary>Transferência entre filiais — par espelhado atômico.</summary>
    [HttpPost("transfers")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Transfer([FromBody] TransferStockCommand command, CancellationToken ct)
    {
        try
        {
            var (outMv, inMv) = await _movements.TransferAsync(
                command.WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
            return StatusCode(StatusCodes.Status201Created, new { outMovement = outMv, inMovement = inMv });
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    /// <summary>Histórico paginado de movimentações (append-only).</summary>
    [HttpGet("movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMovements(
        [FromQuery] Guid? branchId, [FromQuery] Guid? productId, [FromQuery] MovementType? type,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var query = new ListMovementsQuery(branchId, productId, type, page, pageSize)
                .WithTenant(User.GetRequiredTenantId());
            return Ok(await _movements.ListAsync(query, ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }
}

