using System.Security.Claims;
using Estoque.Api.Extensions;
using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>
/// CRUD de Produtos — catálogo do TENANT (disponível para todas as filiais,
/// sem cópia por filial). Escrita: TenantAdmin/Manager. Leitura: qualquer role
/// do tenant (claim tenant_id obrigatória).
/// </summary>
[ApiController]
[Route("api/products")]
[Authorize(Policy = "tenant")]
public sealed class ProductsController : EstoqueControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        try
        {
            var product = await _products.CreateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(GetById), new { productId = product!.Id }, product);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{productId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid productId, [FromBody] UpdateProductCommand body, CancellationToken ct)
    {
        try
        {
            var command = body with { ProductId = productId };
            var product = await _products.UpdateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return product is null ? NotFound("Produto não encontrado.") : Ok(product);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{productId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SoftDelete(Guid productId, CancellationToken ct)
    {
        var deleted = await _products.SoftDeleteAsync(
            new SoftDeleteProductCommand(productId).WithTenant(User.GetRequiredTenantId()), ct);
        return deleted ? NoContent() : NotFound("Produto não encontrado.");
    }

    [HttpGet("{productId:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid productId, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(
            new GetProductByIdQuery(productId).WithTenant(User.GetRequiredTenantId()), ct);
        return product is null ? NotFound("Produto não encontrado.") : Ok(product);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] Guid? categoryId, [FromQuery] Guid? brandId,
        [FromQuery] bool includeInactive = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var query = new ListProductsQuery(search, categoryId, brandId, includeInactive, page, pageSize)
                .WithTenant(User.GetRequiredTenantId());
            return Ok(await _products.ListAsync(query, ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }
}


