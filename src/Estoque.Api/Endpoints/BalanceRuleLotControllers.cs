using Estoque.Api.Extensions;
using Estoque.Application.Common;
using Estoque.Application.Movimentacoes;
using Estoque.Application.Politicas;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>
/// Consultas de saldos, regras de estoque e lotes/validade — leitura para
/// qualquer role do tenant; escrita das regras para TenantAdmin/Manager.
/// </summary>
[ApiController]
[Route("api/balances")]
[Authorize(Policy = "tenant")]
public sealed class BalancesController : EstoqueControllerBase
{
    private readonly IStockBalanceService _balances;

    public BalancesController(IStockBalanceService balances) => _balances = balances;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StockBalanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? branchId, [FromQuery] bool belowMinimumOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var query = new ListBalancesQuery(branchId, belowMinimumOnly, page, pageSize)
                .WithTenant(User.GetRequiredTenantId());
            return Ok(await _balances.ListAsync(query, ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }
}

[ApiController]
[Route("api/stock-rules")]
[Authorize(Policy = "tenant")]
public sealed class StockRulesController : EstoqueControllerBase
{
    private readonly IStockRuleService _rules;

    public StockRulesController(IStockRuleService rules) => _rules = rules;

    /// <summary>Upsert da regra (tenant, produto[, filial]).</summary>
    [HttpPut]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(StockRuleDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Set([FromBody] SetStockRuleCommand command, CancellationToken ct)
    {
        try
        {
            var rule = await _rules.SetAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return Ok(rule);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StockRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _rules.ListAsync(User.GetRequiredTenantId(), page, pageSize, ct));
}

[ApiController]
[Route("api/lots")]
[Authorize(Policy = "tenant")]
public sealed class LotsController : EstoqueControllerBase
{
    private readonly ILotService _lots;

    public LotsController(ILotService lots) => _lots = lots;

    /// <summary>Lotes vencendo nos próximos N dias (ou vencidos com saldo).</summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(PagedResult<LotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExpiring(
        [FromQuery] int days = 30, [FromQuery] Guid? branchId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        try
        {
            var query = new ListExpiringLotsQuery(days, branchId, page, pageSize)
                .WithTenant(User.GetRequiredTenantId());
            return Ok(await _lots.ListExpiringAsync(query, ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }
}

