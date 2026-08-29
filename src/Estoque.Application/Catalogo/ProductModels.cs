using Estoque.Application.Common;
using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using FluentValidation;

namespace Estoque.Application.Catalogo;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Barcode,
    Guid? BrandId,
    Guid? ModelId,
    Guid? CategoryId,
    UnitOfMeasure UnitOfMeasure,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record BrandDto(Guid Id, string Name, bool IsActive);
public sealed record ModelDto(Guid Id, Guid BrandId, string Name, bool IsActive);
public sealed record CategoryDto(Guid Id, string Name, Guid? ParentCategoryId, bool IsActive);

public sealed record SupplierDto(
    Guid Id,
    string Name,
    int TipoPessoa,
    string Documento,
    string Email,
    bool IsActive);

// ---------------------------------------------------------------------------
// Product — Commands/Queries
// ---------------------------------------------------------------------------

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string? Barcode,
    Guid? BrandId,
    Guid? ModelId,
    Guid? CategoryId,
    UnitOfMeasure UnitOfMeasure)
{
    /// <summary>Preenchido pelo controller a partir da claim tenant_id.</summary>
    public CreateProductCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Barcode,
    Guid? BrandId,
    Guid? ModelId,
    Guid? CategoryId,
    UnitOfMeasure UnitOfMeasure)
{
    public UpdateProductCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record SoftDeleteProductCommand(Guid ProductId)
{
    public SoftDeleteProductCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record GetProductByIdQuery(Guid ProductId)
{
    public GetProductByIdQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record ListProductsQuery(
    string? Search = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    bool IncludeInactive = false,
    int Page = 1,
    int PageSize = 20)
{
    public ListProductsQuery WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64)
            .WithMessage("SKU é obrigatório (até 64 caracteres).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.UnitOfMeasure).IsInEnum()
            .WithMessage("Unidade de medida inválida.");
    }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.UnitOfMeasure).IsInEnum();
    }
}

public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
