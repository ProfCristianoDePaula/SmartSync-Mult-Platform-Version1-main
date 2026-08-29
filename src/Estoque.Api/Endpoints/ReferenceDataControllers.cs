using Estoque.Api.Extensions;
using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Endpoints;

/// <summary>
/// CRUD leve de Marcas, Modelos e Categorias — dados de referência do tenant.
/// Escrita: TenantAdmin/Manager. Leitura: qualquer role do tenant.
/// </summary>
[ApiController]
[Route("api/brands")]
[Authorize(Policy = "tenant")]
public sealed class BrandsController : EstoqueControllerBase
{
    private readonly IBrandService _brands;

    public BrandsController(IBrandService brands) => _brands = brands;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(BrandDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateBrandCommand command, CancellationToken ct)
    {
        try
        {
            var brand = await _brands.CreateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(List), brand);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{brandId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Update(Guid brandId, [FromBody] UpdateBrandCommand body, CancellationToken ct)
    {
        try
        {
            var brand = await _brands.UpdateAsync((body with { BrandId = brandId }).WithTenant(User.GetRequiredTenantId()), ct);
            return brand is null ? NotFound("Marca não encontrada.") : Ok(brand);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{brandId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> SoftDelete(Guid brandId, CancellationToken ct)
    {
        var deleted = await _brands.SoftDeleteAsync(
            new SoftDeleteBrandCommand(brandId).WithTenant(User.GetRequiredTenantId()), ct);
        return deleted ? NoContent() : NotFound("Marca não encontrada.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BrandDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _brands.ListAsync(new ListBrandsQuery(page, pageSize).WithTenant(User.GetRequiredTenantId()), ct));
}

[ApiController]
[Route("api/models")]
[Authorize(Policy = "tenant")]
public sealed class ModelsController : EstoqueControllerBase
{
    private readonly IModelService _models;

    public ModelsController(IModelService models) => _models = models;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(ModelDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateModelCommand command, CancellationToken ct)
    {
        try
        {
            var model = await _models.CreateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(List), model);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{modelId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Update(Guid modelId, [FromBody] UpdateModelCommand body, CancellationToken ct)
    {
        try
        {
            var model = await _models.UpdateAsync((body with { ModelId = modelId }).WithTenant(User.GetRequiredTenantId()), ct);
            return model is null ? NotFound("Modelo não encontrado.") : Ok(model);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{modelId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> SoftDelete(Guid modelId, CancellationToken ct)
    {
        var deleted = await _models.SoftDeleteAsync(
            new SoftDeleteModelCommand(modelId).WithTenant(User.GetRequiredTenantId()), ct);
        return deleted ? NoContent() : NotFound("Modelo não encontrado.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ModelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] Guid? brandId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _models.ListAsync(
            new ListModelsQuery(brandId, page, pageSize).WithTenant(User.GetRequiredTenantId()), ct));
}

[ApiController]
[Route("api/categories")]
[Authorize(Policy = "tenant")]
public sealed class CategoriesController : EstoqueControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories) => _categories = categories;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken ct)
    {
        try
        {
            var category = await _categories.CreateAsync(command.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(List), category);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{categoryId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> Update(Guid categoryId, [FromBody] UpdateCategoryCommand body, CancellationToken ct)
    {
        try
        {
            var category = await _categories.UpdateAsync(
                (body with { CategoryId = categoryId }).WithTenant(User.GetRequiredTenantId()), ct);
            return category is null ? NotFound("Categoria não encontrada.") : Ok(category);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{categoryId:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin + "," + PlatformRoles.Manager)]
    public async Task<IActionResult> SoftDelete(Guid categoryId, CancellationToken ct)
    {
        var deleted = await _categories.SoftDeleteAsync(
            new SoftDeleteCategoryCommand(categoryId).WithTenant(User.GetRequiredTenantId()), ct);
        return deleted ? NoContent() : NotFound("Categoria não encontrada.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
        => Ok(await _categories.ListAsync(
            new ListCategoriesQuery(page, pageSize).WithTenant(User.GetRequiredTenantId()), ct));
}


