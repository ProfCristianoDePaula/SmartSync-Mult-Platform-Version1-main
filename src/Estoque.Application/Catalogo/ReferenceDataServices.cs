using Estoque.Application.Common;
using Estoque.Domain.Common;
using FluentValidation;

namespace Estoque.Application.Catalogo;

public interface IBrandService
{
    Task<BrandDto?> CreateAsync(CreateBrandCommand command, CancellationToken ct = default);
    Task<BrandDto?> UpdateAsync(UpdateBrandCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteBrandCommand command, CancellationToken ct = default);
    Task<PagedResult<BrandDto>> ListAsync(ListBrandsQuery query, CancellationToken ct = default);
}

public interface IModelService
{
    Task<ModelDto?> CreateAsync(CreateModelCommand command, CancellationToken ct = default);
    Task<ModelDto?> UpdateAsync(UpdateModelCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteModelCommand command, CancellationToken ct = default);
    Task<PagedResult<ModelDto>> ListAsync(ListModelsQuery query, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<CategoryDto?> CreateAsync(CreateCategoryCommand command, CancellationToken ct = default);
    Task<CategoryDto?> UpdateAsync(UpdateCategoryCommand command, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(SoftDeleteCategoryCommand command, CancellationToken ct = default);
    Task<PagedResult<CategoryDto>> ListAsync(ListCategoriesQuery query, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Supplier
// ---------------------------------------------------------------------------

public sealed record CreateSupplierCommand(
    string Name,
    int TipoPessoa,
    string Documento,
    string Email,
    AddressInput Address,
    ContactInput Contact)
{
    public CreateSupplierCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateSupplierCommand(
    Guid SupplierId,
    string Name,
    string Email,
    AddressInput Address,
    ContactInput Contact)
{
    public UpdateSupplierCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record SoftDeleteSupplierCommand(Guid SupplierId)
{
    public SoftDeleteSupplierCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListSuppliersQuery(string? Search = null, int Page = 1, int PageSize = 20)
{
    public ListSuppliersQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record AddressInput(
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string PostalCode);

public sealed record ContactInput(string Phone, string? SecondaryPhone, string Email);

public sealed class SupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public SupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.TipoPessoa).InclusiveBetween(1, 2)
            .WithMessage("Tipo de pessoa deve ser 1 (Física) ou 2 (Jurídica).");
        RuleFor(x => x.Documento).NotEmpty()
            .WithMessage("Documento (CPF/CNPJ) é obrigatório.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Address).NotNull();
        RuleFor(x => x.Address!.Street).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address!.State).Length(2);
        RuleFor(x => x.Contact).NotNull();
        RuleFor(x => x.Contact!.Phone).NotEmpty();
        RuleFor(x => x.Contact!.Email).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Contact?.Email));
    }
}

