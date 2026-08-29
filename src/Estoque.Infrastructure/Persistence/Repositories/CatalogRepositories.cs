using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(EstoqueDbContext dbContext) : IProductRepository
{
    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await dbContext.Products.AddAsync(product, ct);

    public Task<Product?> GetAsync(TenantId tenantId, Guid productId, CancellationToken ct = default)
        => dbContext.Products.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Id == ProductId.From(productId), ct);

    public Task<Product?> GetBySkuAsync(TenantId tenantId, string sku, CancellationToken ct = default)
        // Igualdade sobre propriedade convertida: o conversor é aplicado à
        // constante — nunca acesse .Value dentro da árvore de expressão.
        => dbContext.Products.FirstOrDefaultAsync(
            p => p.TenantId == tenantId
                 && p.Sku == Domain.ValueObjects.Sku.Create(sku), ct);

    public Task<Product?> GetByBarcodeAsync(TenantId tenantId, string barcode, CancellationToken ct = default)
        => dbContext.Products.FirstOrDefaultAsync(
            p => p.TenantId == tenantId
                 && p.Barcode != null
                 && p.Barcode == Domain.ValueObjects.Barcode.Create(barcode), ct);

    public async Task<(IReadOnlyList<Product> Items, int Total)> ListAsync(
        TenantId tenantId, string? search, Guid? categoryId, Guid? brandId,
        bool includeInactive, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Products.AsNoTracking();

        if (includeInactive)
            query = query.IgnoreQueryFilters(["Active"])
                .Where(p => p.TenantId == tenantId);
        else
            query = query.Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Name.Contains(search) || p.Sku.Value.Contains(search.ToUpperInvariant()));

        if (categoryId is not null)
            query = query.Where(p => p.CategoryId == CategoryId.From(categoryId.Value));
        if (brandId is not null)
            query = query.Where(p => p.BrandId == BrandId.From(brandId.Value));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Product>> ListBySkusAsync(TenantId tenantId, IEnumerable<string> skus, CancellationToken ct = default)
    {
        var normalized = skus.Select(s => s.Trim().ToUpperInvariant()).Distinct().ToList();
        return await dbContext.Products
            .Where(p => p.TenantId == tenantId && normalized.Contains(p.Sku.Value))
            .ToListAsync(ct);
    }
}

public sealed class BrandRepository(EstoqueDbContext dbContext) : IBrandRepository
{
    public async Task AddAsync(Brand brand, CancellationToken ct = default)
        => await dbContext.Brands.AddAsync(brand, ct);

    public Task<Brand?> GetAsync(TenantId tenantId, Guid brandId, CancellationToken ct = default)
        => dbContext.Brands.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == BrandId.From(brandId), ct);

    public Task<Brand?> GetByNameAsync(TenantId tenantId, string name, CancellationToken ct = default)
        => dbContext.Brands.FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Name == name.Trim(), ct);

    public async Task<(IReadOnlyList<Brand> Items, int Total)> ListAsync(
        TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Brands.AsNoTracking().Where(b => b.TenantId == tenantId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class ModelRepository(EstoqueDbContext dbContext) : IModelRepository
{
    public async Task AddAsync(Model model, CancellationToken ct = default)
        => await dbContext.Models.AddAsync(model, ct);

    public Task<Model?> GetAsync(TenantId tenantId, Guid modelId, CancellationToken ct = default)
        => dbContext.Models.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == ModelId.From(modelId), ct);

    public Task<Model?> GetByNameAsync(TenantId tenantId, Guid brandId, string name, CancellationToken ct = default)
        => dbContext.Models.FirstOrDefaultAsync(m =>
            m.TenantId == tenantId && m.BrandId == BrandId.From(brandId) && m.Name == name.Trim(), ct);

    public async Task<(IReadOnlyList<Model> Items, int Total)> ListAsync(
        TenantId tenantId, Guid? brandId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Models.AsNoTracking().Where(m => m.TenantId == tenantId);
        if (brandId is not null)
            query = query.Where(m => m.BrandId == BrandId.From(brandId.Value));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class CategoryRepository(EstoqueDbContext dbContext) : ICategoryRepository
{
    public async Task AddAsync(Category category, CancellationToken ct = default)
        => await dbContext.Categories.AddAsync(category, ct);

    public Task<Category?> GetAsync(TenantId tenantId, Guid categoryId, CancellationToken ct = default)
        => dbContext.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == CategoryId.From(categoryId), ct);

    public Task<Category?> GetByNameAsync(TenantId tenantId, string name, CancellationToken ct = default)
        => dbContext.Categories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Name == name.Trim(), ct);

    public async Task<(IReadOnlyList<Category> Items, int Total)> ListAsync(
        TenantId tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Categories.AsNoTracking().Where(c => c.TenantId == tenantId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}

public sealed class SupplierRepository(EstoqueDbContext dbContext) : ISupplierRepository
{
    public async Task AddAsync(Supplier supplier, CancellationToken ct = default)
        => await dbContext.Suppliers.AddAsync(supplier, ct);

    public Task<Supplier?> GetAsync(TenantId tenantId, Guid supplierId, CancellationToken ct = default)
        => dbContext.Suppliers.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == SupplierId.From(supplierId), ct);

    public Task<Supplier?> GetByDocumentAsync(TenantId tenantId, string documentNumber, CancellationToken ct = default)
        => dbContext.Suppliers.FirstOrDefaultAsync(s =>
            s.TenantId == tenantId && s.DocumentNumber == documentNumber, ct);

    public async Task<(IReadOnlyList<Supplier> Items, int Total)> ListAsync(
        TenantId tenantId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Suppliers.AsNoTracking().Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
