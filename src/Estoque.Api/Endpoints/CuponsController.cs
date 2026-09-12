using Estoque.Api.Extensions;
using Estoque.Application.PedidosVendas;
using Estoque.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

[ApiController]
[Route("api/cupons")]
[Authorize(Policy = "tenant")]
public sealed class CuponsController : EstoqueControllerBase
{
    private readonly ICupomService _cupons;

    public CuponsController(ICupomService cupons) => _cupons = cupons;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(CupomDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCupomCommand command, CancellationToken ct)
    {
        try
        {
            var cupom = await _cupons.CreateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(GetById), new { id = cupom.Id }, cupom);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCupomCommand body, CancellationToken ct)
    {
        try
        {
            var cmd = body with { CupomId = id };
            var cupom = await _cupons.UpdateAsync(cmd.WithTenant(User.GetRequiredTenantId()), ct);
            return cupom is null ? NotFound("Cupom não encontrado.") : Ok(cupom);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _cupons.DeleteAsync(User.GetRequiredTenantId(), id, ct);
        return ok ? NoContent() : NotFound("Cupom não encontrado.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var cupom = await _cupons.GetByIdAsync(new GetCupomByIdQuery(id).WithTenant(User.GetRequiredTenantId()), ct);
        return cupom is null ? NotFound("Cupom não encontrado.") : Ok(cupom);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool? apenasValidos, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _cupons.ListAsync(new ListCuponsQuery(search, apenasValidos, page, pageSize).WithTenant(User.GetRequiredTenantId()), ct));
}
