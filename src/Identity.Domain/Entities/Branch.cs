using Identity.Domain.Common;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

/// <summary>
/// Branch — filial de um tenant. Sempre associada a um TenantId. Suporta soft
/// delete (Etapa 14): <see cref="IsActive"/> + <see cref="DeletedAtUtc"/> são
/// gerenciados pelo named query filter "Active" na configuração EF Core.
/// </summary>
public sealed class Branch : Entity<BranchId>
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public Contact Contact { get; private set; } = null!;

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Branch() { }

    private Branch(BranchId id, TenantId tenantId, string name, Address address, Contact contact)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        Address = address;
        Contact = contact;
        IsActive = true;
    }

    public static Branch Create(TenantId tenantId, string name, Address address, Contact contact)
        => new(BranchId.New(), tenantId, name, address, contact);

    public void Update(string name, Address address, Contact contact)
    {
        SetName(name);
        Address = address ?? throw new ArgumentNullException(nameof(address));
        Contact = contact ?? throw new ArgumentNullException(nameof(contact));
    }

    public void SoftDelete()
    {
        if (!IsActive)
            return;

        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nome da filial é obrigatório.", nameof(name));
        Name = name.Trim();
    }

    public void SetAddress(Address address)
        => Address = address ?? throw new ArgumentNullException(nameof(address));

    public void SetContact(Contact contact)
        => Contact = contact ?? throw new ArgumentNullException(nameof(contact));
}