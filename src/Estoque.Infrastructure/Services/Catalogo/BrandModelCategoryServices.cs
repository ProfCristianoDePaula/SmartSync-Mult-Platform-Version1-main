using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;

namespace Estoque.Infrastructure.Services.Catalogo;

public sealed class BrandService(IBrandRepository brands, IUnitOfWork uow) : IBrandService
{
    public async Task<BrandDto?> CreateAsync(CreateBrandCommand command, CancellationToken ct = default)
    {
        var existing = await brands.GetByNameAsync(command.TenantId, command.Name, ct);
        if (existing is not null)
            throw new BusinessRuleViolationException("Já existe uma marca com este nome.");

        var brand = Brand.Create(command.TenantId, command.Name);
        await brands.AddAsync(brand, ct);
        await uow.SaveChangesAsync(ct);
        return new BrandDto(brand.Id.Value, brand.Name, brand.IsActive);
    }

    public async Task<BrandDto?> UpdateAsync(UpdateBrandCommand command, CancellationToken ct = default)
    {
        var brand = await brands.GetAsync(command.TenantId, command.BrandId, ct);
        if (brand is null) return null;

        var duplicate = await brands.GetByNameAsync(command.TenantId, command.Name, ct);
        if (duplicate is not null && duplicate.Id != brand.Id)
            throw new BusinessRuleViolationException("Já existe uma marca com este nome.");

        brand.Update(command.Name);
        await uow.SaveChangesAsync(ct);
        return new BrandDto(brand.Id.Value, brand.Name, brand.IsActive);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteBrandCommand command, CancellationToken ct = default)
    {
        var brand = await brands.GetAsync(command.TenantId, command.BrandId, ct);
        if (brand is null || !brand.IsActive) return false;
        brand.SoftDelete();
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<BrandDto>> ListAsync(ListBrandsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await brands.ListAsync(query.TenantId, query.Page, query.PageSize, ct);
        return new PagedResult<BrandDto>(
            items.Select(b => new BrandDto(b.Id.Value, b.Name, b.IsActive)).ToList(),
            query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }
}

public sealed class ModelService(IModelRepository models, IUnitOfWork uow) : IModelService
{
    public async Task<ModelDto?> CreateAsync(CreateModelCommand command, CancellationToken ct = default)
    {
        var existing = await models.GetByNameAsync(command.TenantId, command.BrandId, command.Name, ct);
        if (existing is not null)
            throw new BusinessRuleViolationException("Já existe um modelo com este nome nesta marca.");

        var model = Model.Create(command.TenantId, command.BrandId, command.Name);
        await models.AddAsync(model, ct);
        await uow.SaveChangesAsync(ct);
        return new ModelDto(model.Id.Value, model.BrandId.Value, model.Name, model.IsActive);
    }

    public async Task<ModelDto?> UpdateAsync(UpdateModelCommand command, CancellationToken ct = default)
    {
        var model = await models.GetAsync(command.TenantId, command.ModelId, ct);
        if (model is null) return null;
        model.Update(command.Name);
        await uow.SaveChangesAsync(ct);
        return new ModelDto(model.Id.Value, model.BrandId.Value, model.Name, model.IsActive);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteModelCommand command, CancellationToken ct = default)
    {
        var model = await models.GetAsync(command.TenantId, command.ModelId, ct);
        if (model is null || !model.IsActive) return false;
        model.SoftDelete();
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<ModelDto>> ListAsync(ListModelsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await models.ListAsync(query.TenantId, query.BrandId, query.Page, query.PageSize, ct);
        return new PagedResult<ModelDto>(
            items.Select(m => new ModelDto(m.Id.Value, m.BrandId.Value, m.Name, m.IsActive)).ToList(),
            query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }
}

public sealed class CategoryService(ICategoryRepository categories, IUnitOfWork uow) : ICategoryService
{
    public async Task<CategoryDto?> CreateAsync(CreateCategoryCommand command, CancellationToken ct = default)
    {
        var existing = await categories.GetByNameAsync(command.TenantId, command.Name, ct);
        if (existing is not null)
            throw new BusinessRuleViolationException("Já existe uma categoria com este nome.");

        var category = Category.Create(command.TenantId, command.Name, command.ParentCategoryId);
        await categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<CategoryDto?> UpdateAsync(UpdateCategoryCommand command, CancellationToken ct = default)
    {
        var category = await categories.GetAsync(command.TenantId, command.CategoryId, ct);
        if (category is null) return null;
        category.Update(command.Name, command.ParentCategoryId);
        await uow.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<bool> SoftDeleteAsync(SoftDeleteCategoryCommand command, CancellationToken ct = default)
    {
        var category = await categories.GetAsync(command.TenantId, command.CategoryId, ct);
        if (category is null || !category.IsActive) return false;
        category.SoftDelete();
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedResult<CategoryDto>> ListAsync(ListCategoriesQuery query, CancellationToken ct = default)
    {
        var (items, total) = await categories.ListAsync(query.TenantId, query.Page, query.PageSize, ct);
        return new PagedResult<CategoryDto>(
            items.Select(ToDto).ToList(), query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private static CategoryDto ToDto(Category c)
        => new(c.Id.Value, c.Name, c.ParentCategoryId?.Value, c.IsActive);
}
