using Fiscal.Api.Extensions;
using Fiscal.Application.Emitentes;
using Fiscal.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Concessões por unidade: TenantAdmin (próprio tenant) concede/revoga.
/// </summary>
[ApiController]
[Route("api/fiscal/concessoes")]
[Authorize(Policy = "tenant")]
[Authorize(Roles = PlatformRoles.TenantAdmin)]
public sealed class ConcessoesController : FiscalControllerBase
{
    private readonly IConcessaoService _concessoes;

    public ConcessoesController(IConcessaoService concessoes) => _concessoes = concessoes;

    [HttpPost]
    public async Task<IActionResult> Grant([FromBody] GrantConcessaoCommand command, CancellationToken ct)
    {
        try
        {
            var dto = await _concessoes.GrantAsync(
                command.WithTenant(User.GetRequiredTenantId()), User.GetUserId(), ct);
            return CreatedAtAction(nameof(List), dto);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? branchId, CancellationToken ct)
        => Ok(await _concessoes.ListAsync(User.GetRequiredTenantId(), branchId, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var ok = await _concessoes.RevokeAsync(User.GetRequiredTenantId(), id, ct);
        return ok ? NoContent() : NotFound("Concessão não encontrada.");
    }
}
