using Estoque.Api.Extensions;
using Estoque.Application.AutoCompra;
using Estoque.Application.Common;
using Estoque.Application.Politicas;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>Outlet: marcação de itens avariados/devolvidos/vencendo por filial.</summary>
[ApiController]
[Route("api/outlet-items")]
[Authorize(Policy = "tenant")]
public sealed class OutletItemsController : EstoqueControllerBase
{
    private readonly IOutletItemService _outlet;

    public OutletItemsController(IOutletItemService outlet) => _outlet = outlet;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager + "," + PlatformRoles.Seller)]
    [ProducesResponseType(typeof(OutletDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Mark([FromBody] MarkOutletCommand command, CancellationToken ct)
    {
        try
        {
            var outlet = await _outlet.MarkAsync(
                command.WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
            return CreatedAtAction(nameof(ListOpen), new { }, outlet);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    /// <summary>Baixa (resolução) de um item de outlet.</summary>
    [HttpPatch("{outletItemId:guid}/resolve")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Resolve(Guid outletItemId, CancellationToken ct)
    {
        var resolved = await _outlet.ResolveAsync(
            new ResolveOutletCommand(outletItemId).WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
        return resolved ? NoContent() : NotFound("Item de outlet não encontrado ou já resolvido.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OutletDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOpen(
        [FromQuery] Guid? branchId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _outlet.ListOpenAsync(User.GetRequiredTenantId(), branchId, page, pageSize, ct));
}

/// <summary>Sugestões de AutoCompra geradas pelo motor de reposição.</summary>
[ApiController]
[Route("api/purchase-suggestions")]
[Authorize(Policy = "tenant")]
public sealed class PurchaseSuggestionsController : EstoqueControllerBase
{
    private readonly IPurchaseSuggestionService _suggestions;

    public PurchaseSuggestionsController(IPurchaseSuggestionService suggestions) => _suggestions = suggestions;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PurchaseSuggestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool openOnly = true, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _suggestions.ListAsync(
            new ListSuggestionsQuery(openOnly, page, pageSize).WithTenant(User.GetRequiredTenantId()), ct));

    /// <summary>Aprova ou descarta uma sugestão ABERTA.</summary>
    [HttpPatch("{suggestionId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Decide(Guid suggestionId, [FromBody] DecideRequest body, CancellationToken ct)
    {
        var decided = await _suggestions.DecideAsync(
            new DecideSuggestionCommand(suggestionId, body.Approve)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
        return decided ? NoContent() : NotFound("Sugestão não encontrada.");
    }

    public sealed record DecideRequest(bool Approve);
}

