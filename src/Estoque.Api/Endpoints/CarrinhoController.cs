using Estoque.Api.Extensions;
using Estoque.Application.PedidosVendas;
using Estoque.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

[ApiController]
[Route("api/carrinho")]
[Authorize(Policy = "tenant")]
public sealed class CarrinhoController : EstoqueControllerBase
{
    private readonly IPedidoService _pedidos;
    private readonly ICheckoutService _checkout;

    public CarrinhoController(IPedidoService pedidos, ICheckoutService checkout)
    {
        _pedidos = pedidos;
        _checkout = checkout;
    }

    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] Guid? idUnidade, CancellationToken ct)
    {
        var pedido = await _pedidos.ConsultarCarrinhoAsync(
            new ConsultarCarrinhoQuery(idUnidade).WithContext(User.GetRequiredTenantId(), User.GetUserId()), ct);
        return pedido is null ? NotFound("Carrinho não encontrado.") : Ok(pedido);
    }

    [HttpPost("itens")]
    public async Task<IActionResult> AdicionarItem([FromBody] AdicionarItemRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new AdicionarItemCommand(req.ProdutoId, req.Quantidade, req.IdUnidade)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId());
            var pedido = await _pedidos.AdicionarItemAsync(cmd, ct);
            return Ok(pedido);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("itens/{produtoId:guid}")]
    public async Task<IActionResult> RemoverItem(Guid produtoId, [FromQuery] Guid? idUnidade, CancellationToken ct)
    {
        try
        {
            var cmd = new RemoverItemCommand(produtoId, idUnidade)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId());
            var pedido = await _pedidos.RemoverItemAsync(cmd, ct);
            return Ok(pedido);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPost("cupom")]
    public async Task<IActionResult> AplicarCupom([FromBody] AplicarCupomRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new AplicarCupomCarrinhoCommand(req.CupomId, req.IdUnidade)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId());
            var pedido = await _pedidos.AplicarCupomAsync(cmd, ct);
            return Ok(pedido);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPost("calcular-frete")]
    public async Task<IActionResult> CalcularFrete([FromBody] CalcularFreteRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new CalcularFreteCommand(req.Cep, req.IdUnidade)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId());
            var res = await _checkout.CalcularFreteAsync(cmd, ct);
            return Ok(res);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest req, CancellationToken ct)
    {
        try
        {
            var cmd = new FinalizarCheckoutCommand(req.Cep, req.IdFormaPagto, req.QuantidadeParcelas, req.IdUnidade)
                .WithContext(User.GetRequiredTenantId(), User.GetUserId());
            var venda = await _checkout.FinalizarAsync(cmd, ct);
            return Ok(venda);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    public sealed record AdicionarItemRequest(Guid ProdutoId, decimal Quantidade, Guid? IdUnidade);
    public sealed record AplicarCupomRequest(Guid CupomId, Guid? IdUnidade);
    public sealed record CalcularFreteRequest(string Cep, Guid? IdUnidade);
    public sealed record CheckoutRequest(string Cep, Guid IdFormaPagto, int QuantidadeParcelas, Guid? IdUnidade);
}
