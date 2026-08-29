using Estoque.Domain.Common;
using FluentValidation;

namespace Estoque.Application.Catalogo;

// ---------------------------------------------------------------------------
// Brand
// ---------------------------------------------------------------------------

public sealed record CreateBrandCommand(string Name)
{
    public CreateBrandCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateBrandCommand(Guid BrandId, string Name)
{
    public UpdateBrandCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record SoftDeleteBrandCommand(Guid BrandId)
{
    public SoftDeleteBrandCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListBrandsQuery(int Page = 1, int PageSize = 50)
{
    public ListBrandsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed class BrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public BrandCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

public sealed class UpdateBrandCommandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator()
    {
        RuleFor(x => x.BrandId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

// ---------------------------------------------------------------------------
// Model
// ---------------------------------------------------------------------------

public sealed record CreateModelCommand(Guid BrandId, string Name)
{
    public CreateModelCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateModelCommand(Guid ModelId, Guid BrandId, string Name)
{
    public UpdateModelCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record SoftDeleteModelCommand(Guid ModelId)
{
    public SoftDeleteModelCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListModelsQuery(Guid? BrandId = null, int Page = 1, int PageSize = 50)
{
    public ListModelsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed class ModelCommandValidator : AbstractValidator<CreateModelCommand>
{
    public ModelCommandValidator()
    {
        RuleFor(x => x.BrandId).NotEqual(Guid.Empty).WithMessage("Marca é obrigatória.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

// ---------------------------------------------------------------------------
// Category
// ---------------------------------------------------------------------------

public sealed record CreateCategoryCommand(string Name, Guid? ParentCategoryId)
{
    public CreateCategoryCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateCategoryCommand(Guid CategoryId, string Name, Guid? ParentCategoryId)
{
    public UpdateCategoryCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record SoftDeleteCategoryCommand(Guid CategoryId)
{
    public SoftDeleteCategoryCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListCategoriesQuery(int Page = 1, int PageSize = 100)
{
    public ListCategoriesQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed class CategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
    }
}

