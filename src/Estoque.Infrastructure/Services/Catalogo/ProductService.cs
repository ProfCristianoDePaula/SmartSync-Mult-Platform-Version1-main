using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Infrastructure.Persistence.Outbox;

namespace Estoque.Infrastructure.Services.Catalogo;

public sealed class ProductService(
    IProductRepository products,
    IUnitOfWork uow,
    OutboxService outbox) : IProductService
{
    public async Task<ProductDto?> CreateAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        var tenantId = command.TenantId;
        var sku = Domain.ValueObjects.Sku.Create(command.Sku);

        var existing = await products.GetBySkuAsync(tenantId, sku.Value, ct);
        if (existing is not null && existing.IsActive)
            throw new BusinessRuleViolationException($"SKU '{sku}' já está em uso neste tenant.");

        if (command.Barcode is not null)
        {
            var barcode = Domain.ValueObjects.Barcode.Create(command.Barcode);
            var byBarcode = await products.GetByBarcodeAsync(tenantId, barcode.Value, ct);
            if (byBarcode is not null && byBarcode.IsActive)
                throw new BusinessRuleViolationException("Código de barras já está em uso neste tenant.");
        }

        var product = Product.Create(
            tenantId, sku.Value, command.Name, command.Barcode,
            ToId<BrandId>(command.BrandId), ToId<ModelId>(command.ModelId), ToId<CategoryId>(command.CategoryId),
            command.UnitOfMeasure);

        await products.AddAsync(product, ct);
        await outbox.WriteAsync(product.PopEvents(), ct);
        await uow.SaveChangesAsync(ct);

        return ToDto(product);
    }

    public async Task<ProductDto?> UpdateAsync(UpdateProductCommand command, CancellationToken ct = default)
    {
        var product = await products.GetAsync(command.TenantId, command.ProductId, ct);
        if (product is null || !product.IsActive)
            return null;

        product.Update(command.Name, command.Barcode,
                       ToId<BrandId>(command.BrandId), ToId<ModelId>(command.ModelId),
                       ToId<CategoryId>(command.CategoryId), command.UnitOfMeasure);
        await uow.SaveChangesAsync(ct);

        return ToDto(product);
    }

    /// <summary>Converte Guid? para o ID tipado correspondente (null-preserving).</summary>
    private static TId? ToId<TId>(Guid? value) where TId : struct
        => value is null ? null : (TId?)Activator.CreateInstance(typeof(TId), value.Value);

    public async Task<bool> SoftDeleteAsync(SoftDeleteProductCommand command, CancellationToken ct = default)
    {
        var product = await products.GetAsync(command.TenantId, command.ProductId, ct);
        if (product is null || !product.IsActive)
            return false;

        product.SoftDelete();
        await outbox.WriteAsync(product.PopEvents(), ct);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ProductDto?> GetByIdAsync(GetProductByIdQuery query, CancellationToken ct = default)
    {
        var product = await products.GetAsync(query.TenantId, query.ProductId, ct);
        return product is null ? null : ToDto(product);
    }

    public async Task<PagedResult<ProductDto>> ListAsync(ListProductsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await products.ListAsync(
            query.TenantId, query.Search, query.CategoryId, query.BrandId,
            query.IncludeInactive, query.Page, query.PageSize, ct);

        return new PagedResult<ProductDto>(
            items.Select(ToDto).ToList(), query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private static ProductDto ToDto(Product p) => new(
        p.Id.Value, p.Sku.Value, p.Name, p.Barcode?.Value,
        p.BrandId?.Value, p.ModelId?.Value, p.CategoryId?.Value,
        p.UnitOfMeasure, p.IsActive, p.CreatedAtUtc);
}
