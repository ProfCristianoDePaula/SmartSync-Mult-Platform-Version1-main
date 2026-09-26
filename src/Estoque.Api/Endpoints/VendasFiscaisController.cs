using Estoque.Api.Extensions;
using Estoque.Application.FiscalBridge;
using Estoque.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>
/// Ponte Vendas→Fiscal (Fiscal-13, aditiva): monta o pedido de emissão e
/// chama o Fiscal repassando o JWT. Idempotency-Key = venda + tipo.
/// </summary>
[ApiController]
[Route("api/vendas")]
[Authorize(Policy = "tenant")]
public sealed class VendasFiscaisController : EstoqueControllerBase
{
    private readonly INotaFiscalBridgeService _bridge;

    public VendasFiscaisController(INotaFiscalBridgeService bridge) => _bridge = bridge;

    [HttpPost("{id:guid}/emitir-nota")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    [ProducesResponseType(typeof(NotaFiscalVendaDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> EmitirNota(
        Guid id, [FromBody] EmitirNotaBody body, CancellationToken ct)
    {
        try
        {
            var request = new EmitirNotaRequest(body.Tipo)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId());
            var dto = await _bridge.EmitirNotaAsync(id, request, ct);
            return AcceptedAtAction(nameof(NotaFiscal), new { id }, dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{id:guid}/nota-fiscal")]
    [ProducesResponseType(typeof(NotaFiscalVendaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> NotaFiscal(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _bridge.ConsultarAsync(id, User.GetRequiredTenantId(), ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    public sealed record EmitirNotaBody(TipoDocumentoFiscalBridge Tipo);
}
