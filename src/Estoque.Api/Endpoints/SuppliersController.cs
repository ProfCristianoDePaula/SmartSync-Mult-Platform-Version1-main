using Estoque.Api.Extensions;
using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>
/// CRUD de Fornecedores — documento único por tenant; documento imutável no PUT
/// (mesmo padrão do Tenant do Identity).
/// </summary>
[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = "tenant")]
public sealed class SuppliersController : EstoqueControllerBase
{
    private readonly ISupplierService _suppliers;

    public SuppliersController(ISupplierService suppliers) => _suppliers = suppliers;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierCommand command, CancellationToken ct)
    {
        try
        {
            var supplier = await _suppliers.CreateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(List), supplier);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{supplierId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Update(Guid supplierId, [FromBody] UpdateSupplierCommand body, CancellationToken ct)
    {
        try
        {
            var supplier = await _suppliers.UpdateAsync(
                (body with { SupplierId = supplierId }).WithTenant(User.GetRequiredTenantId()), ct);
            return supplier is null ? NotFound("Fornecedor não encontrado.") : Ok(supplier);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{supplierId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> SoftDelete(Guid supplierId, CancellationToken ct)
    {
        var deleted = await _suppliers.SoftDeleteAsync(
            new SoftDeleteSupplierCommand(supplierId).WithTenant(User.GetRequiredTenantId()), ct);
        return deleted ? NoContent() : NotFound("Fornecedor não encontrado.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _suppliers.ListAsync(
            new ListSuppliersQuery(search, page, pageSize).WithTenant(User.GetRequiredTenantId()), ct));
}


