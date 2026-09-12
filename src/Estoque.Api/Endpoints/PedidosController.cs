using Estoque.Api.Extensions;
using Estoque.Application.PedidosVendas;
using Estoque.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

[ApiController]
[Route("api/pedidos")]
[Authorize(Policy = "tenant")]
public sealed class PedidosController : EstoqueControllerBase
{
    private readonly IPedidoService _pedidos;

    public PedidosController(IPedidoService pedidos) => _pedidos = pedidos;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var pedido = await _pedidos.GetByIdAsync(User.GetRequiredTenantId(), id, ct);
        return pedido is null ? NotFound("Pedido não encontrado.") : Ok(pedido);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? idCliente,
        [FromQuery] Guid? idUnidade,
        [FromQuery] int? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var statusEnum = status is null ? null : (Estoque.Domain.Enums.PedidoStatus?)status.Value;
            var query = new ListPedidosQuery(idCliente, idUnidade, statusEnum, page, pageSize)
                .WithTenant(User.GetRequiredTenantId());
            return Ok(await _pedidos.ListAsync(query, ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        try
        {
            var pedido = await _pedidos.CancelarAsync(User.GetRequiredTenantId(), id, ct);
            return Ok(pedido);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }
}
