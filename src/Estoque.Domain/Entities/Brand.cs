using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>Marca de produto — nome único por tenant entre ativas.</summary>
public sealed class Brand : Entity<BrandId>
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Brand() { }

    private Brand(BrandId id, TenantId tenantId, string name) : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        IsActive = true;
    }

    public static Brand Create(TenantId tenantId, string name) => new(BrandId.New(), tenantId, name);

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
            throw new ArgumentException("Nome da marca é obrigatório.", nameof(name));
        if (name.Trim().Length > 255)
            throw new ArgumentException("Nome da marca excede 255 caracteres.", nameof(name));
        Name = name.Trim();
    }
}
