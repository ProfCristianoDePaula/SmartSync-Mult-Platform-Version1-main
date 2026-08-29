using Estoque.Application.Common;
using Estoque.Domain.Common;

namespace Estoque.Application.Catalogo;

public interface ISupplierService
{
    Task<SupplierDto?> CreateAsync(CreateSupplierCommand command, CancellationToken ct = default);
    Task<SupplierDto?> UpdateAsync(UpdateSupplierCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteSupplierCommand command, CancellationToken ct = default);
    Task<PagedResult<SupplierDto>> ListAsync(ListSuppliersQuery query, CancellationToken ct = default);
}
