using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>Modelo de produto — sempre vinculado a uma marca; nome único por (tenant, marca).</summary>
public sealed class Model : Entity<ModelId>
{
    public TenantId TenantId { get; private set; }
    public BrandId BrandId { get; private set; }
    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Model() { }

    private Model(ModelId id, TenantId tenantId, BrandId brandId, string name) : base(id)
    {
        TenantId = tenantId;
        BrandId = brandId;
        SetName(name);
        IsActive = true;
    }

    public static Model Create(TenantId tenantId, Guid brandId, string name)
        => new(ModelId.New(), tenantId, Common.BrandId.From(brandId), name);

    public void Update(string name) => SetName(name);

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome do modelo é obrigatório.", nameof(name));
        if (name.Trim().Length > 255)
            throw new ArgumentException("Nome do modelo excede 255 caracteres.", nameof(name));
        Name = name.Trim();
    }
}

