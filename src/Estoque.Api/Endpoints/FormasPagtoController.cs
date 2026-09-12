using Estoque.Api.Extensions;
using Estoque.Application.PedidosVendas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

[ApiController]
[Route("api/formas-pagto")]
[Authorize(Policy = "tenant")]
public sealed class FormasPagtoController : EstoqueControllerBase
{
    private readonly IFormaPagtoService _service;

    public FormasPagtoController(IFormaPagtoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));
}
