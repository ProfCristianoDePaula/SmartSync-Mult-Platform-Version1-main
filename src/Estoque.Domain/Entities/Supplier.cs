using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Fornecedor — documento (CPF/CNPJ) único por TENANT entre ativos
/// (índice parcial composto; espelha a regra de tenants do Identity).
/// Persistência achatada (tipo_pessoa + document_number) para permitir o
/// índice composto com tenant_id — a validação continua nos VOs Cpf/Cnpj.
/// </summary>
public sealed class Supplier : Entity<SupplierId>
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>1 = Física (CPF), 2 = Jurídica (CNPJ).</summary>
    public TipoPessoa TipoPessoa { get; private set; }

    /// <summary>Número já validado e normalizado (somente dígitos).</summary>
    public string DocumentNumber { get; private set; } = null!;

    public Email Email { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public Contact Contact { get; private set; } = null!;

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Supplier() { }

    private Supplier(SupplierId id, TenantId tenantId, string name, TipoPessoa tipo,
        string documentNumber, Email email, Address address, Contact contact) : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        TipoPessoa = tipo;
        DocumentNumber = documentNumber;
        Email = email;
        Address = address;
        Contact = contact;
        IsActive = true;
    }

    public static Supplier Create(TenantId tenantId, string name,
        int tipoPessoa, string document, string email,
        Address address, Contact contact)
    {
        if (!Enum.IsDefined(typeof(TipoPessoa), tipoPessoa))
            throw new ArgumentException("Tipo de pessoa inválido (1 = Física, 2 = Jurídica).", nameof(tipoPessoa));

        var tipo = (Enums.TipoPessoa)tipoPessoa;

        // Validação pelos VOs — um documento inválido nunca chega ao banco.
        var number = tipo == Enums.TipoPessoa.Fisica
            ? Cpf.Create(document).Number
            : Cnpj.Create(document).Number;

        return new Supplier(SupplierId.New(), tenantId, name, tipo, number,
                            ValueObjects.Email.Create(email), address, contact);
    }

    public void Update(string name, string email, Address address, Contact contact)
    {
        SetName(name);
        Email = Email.Create(email);
        Address = address ?? throw new ArgumentNullException(nameof(address));
        Contact = contact ?? throw new ArgumentNullException(nameof(contact));
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
            throw new ArgumentException("Nome do fornecedor é obrigatório.", nameof(name));
        if (name.Trim().Length > 255)
            throw new ArgumentException("Nome do fornecedor excede 255 caracteres.", nameof(name));
        Name = name.Trim();
    }
}


