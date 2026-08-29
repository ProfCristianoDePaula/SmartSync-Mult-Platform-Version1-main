using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>Categoria de produto — hierarquia opcional via ParentCategoryId (mesmo tenant).</summary>
public sealed class Category : Entity<CategoryId>
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public CategoryId? ParentCategoryId { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Category() { }

    private Category(CategoryId id, TenantId tenantId, string name, CategoryId? parent) : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        ParentCategoryId = parent;
        IsActive = true;
    }

    public static Category Create(TenantId tenantId, string name, Guid? parentCategoryId)
        => new(CategoryId.New(), tenantId, name,
               parentCategoryId is null ? null : Common.CategoryId.From(parentCategoryId.Value));

    public void Update(string name, Guid? parentCategoryId)
    {
        SetName(name);
        if (parentCategoryId == Id.Value)
            throw new BusinessRuleViolationException("Uma categoria não pode ser pai de si mesma.");
        ParentCategoryId = parentCategoryId is null ? null : Common.CategoryId.From(parentCategoryId.Value);
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome da categoria é obrigatório.", nameof(name));
        if (name.Trim().Length > 255)
            throw new ArgumentException("Nome da categoria excede 255 caracteres.", nameof(name));
        Name = name.Trim();
    }
}

