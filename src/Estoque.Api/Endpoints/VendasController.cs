using Estoque.Api.Extensions;
using Estoque.Application.PedidosVendas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

[ApiController]
[Route("api/vendas")]
[Authorize(Policy = "tenant")]
public sealed class VendasController : EstoqueControllerBase
{
    private readonly IVendaService _vendas;

    public VendasController(IVendaService vendas) => _vendas = vendas;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var venda = await _vendas.GetByIdAsync(new GetVendaByIdQuery(id).WithTenant(User.GetRequiredTenantId()), ct);
        return venda is null ? NotFound("Venda não encontrada.") : Ok(venda);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? idCliente,
        [FromQuery] Guid? idUnidade,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ListVendasQuery(idCliente, idUnidade, page, pageSize).WithTenant(User.GetRequiredTenantId());
        return Ok(await _vendas.ListAsync(query, ct));
    }
}
