using Estoque.Application.Common;
using Estoque.Domain.Common;

namespace Estoque.Application.Catalogo;

public interface IProductService
{
    Task<ProductDto?> CreateAsync(CreateProductCommand command, CancellationToken ct = default);
    Task<ProductDto?> UpdateAsync(UpdateProductCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteProductCommand command, CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(GetProductByIdQuery query, CancellationToken ct = default);
    Task<PagedResult<ProductDto>> ListAsync(ListProductsQuery query, CancellationToken ct = default);
}
