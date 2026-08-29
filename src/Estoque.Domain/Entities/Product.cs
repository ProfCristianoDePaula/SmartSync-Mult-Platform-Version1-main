using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Estoque.Domain.Events;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Produto do CATÁLOGO — pertence ao TENANT (não à filial). Um produto
/// cadastrado está automaticamente disponível para todas as filiais do
/// tenant: NUNCA se copia produto por filial (decisão estruturante).
/// Saldos/movimentações/lotes/regras/outlet é que são por filial.
/// </summary>
public sealed class Product : Entity<ProductId>, IHasDomainEvents
{
    private readonly List<object> _events = [];

    public TenantId TenantId { get; private set; }
    public Sku Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Barcode? Barcode { get; private set; }
    public BrandId? BrandId { get; private set; }
    public ModelId? ModelId { get; private set; }
    public CategoryId? CategoryId { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<object> DomainEvents => _events;

    private Product() { }

    private Product(
        ProductId id,
        TenantId tenantId,
        Sku sku,
        string name,
        Barcode? barcode,
        BrandId? brandId,
        ModelId? modelId,
        CategoryId? categoryId,
        UnitOfMeasure uom)
        : base(id)
    {
        TenantId = tenantId;
        Sku = sku;
        SetName(name);
        Barcode = barcode;
        BrandId = brandId;
        ModelId = modelId;
        CategoryId = categoryId;
        UnitOfMeasure = uom;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Product Create(
        TenantId tenantId,
        string sku,
        string name,
        string? barcode,
        BrandId? brandId,
        ModelId? modelId,
        CategoryId? categoryId,
        UnitOfMeasure uom)
    {
        var product = new Product(
            ProductId.New(), tenantId, ValueObjects.Sku.Create(sku), name,
            barcode is null ? null : ValueObjects.Barcode.Create(barcode),
            brandId, modelId, categoryId, uom);

        product.Raise(new ProductCreated(product.Id, tenantId, product.Sku.Value));
        return product;
    }

    public void Update(string name, string? barcode, BrandId? brandId, ModelId? modelId,
        CategoryId? categoryId, UnitOfMeasure uom)
    {
        SetName(name);
        Barcode = barcode is null ? null : ValueObjects.Barcode.Create(barcode);
        BrandId = brandId;
        ModelId = modelId;
        CategoryId = categoryId;
        UnitOfMeasure = uom;
    }

    public void SoftDelete()
    {
        if (!IsActive)
            return;

        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
        Raise(new ProductSoftDeleted(Id, TenantId));
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome do produto é obrigatório.", nameof(name));
        if (name.Trim().Length > 255)
            throw new ArgumentException("Nome do produto excede 255 caracteres.", nameof(name));
        Name = name.Trim();
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}

