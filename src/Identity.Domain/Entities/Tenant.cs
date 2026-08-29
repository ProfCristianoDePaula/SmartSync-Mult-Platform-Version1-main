using Identity.Domain.Common;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Domain.Entities;

/// <summary>
/// Tenant — entidade raiz (aggregate root) que representa um cliente
/// (pessoa física ou jurídica) da plataforma.
/// </summary>
public sealed class Tenant : Entity<TenantId>
{
    private readonly List<Branch> _branches = [];

    public string LegalName { get; private set; } = null!;
    public string TradeName { get; private set; } = null!;
    public Documento Documento { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public TenantStatus Status { get; private set; }

    /// <summary>Soft delete (Etapa 13): tenants inativados ficam fora das consultas (query filter).</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();

    private Tenant() { }

    private Tenant(
        TenantId id,
        string legalName,
        string tradeName,
        Documento documento,
        Email email)
        : base(id)
    {
        SetLegalName(legalName);
        SetTradeName(tradeName);
        Documento = documento;
        Email = email;
        Status = TenantStatus.Active;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Tenant Create(
        string legalName,
        string tradeName,
        Documento documento,
        Email email)
        => new(TenantId.New(), legalName, tradeName, documento, email);

    public void SetLegalName(string legalName)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            throw new ArgumentException("Razão social é obrigatória.", nameof(legalName));
        LegalName = legalName.Trim();
    }

    public void SetTradeName(string tradeName)
    {
        if (string.IsNullOrWhiteSpace(tradeName))
            throw new ArgumentException("Nome fantasia é obrigatório.", nameof(tradeName));
        TradeName = tradeName.Trim();
    }

    public void SetEmail(Email email)
        => Email = email ?? throw new ArgumentNullException(nameof(email));

    public void Activate() => Status = TenantStatus.Active;
    public void Deactivate() => Status = TenantStatus.Inactive;
    public void Suspend() => Status = TenantStatus.Suspended;

    /// <summary>
    /// Atualização completa do tenant a partir de um comando de edição (Etapa 13).
    /// O documento (CPF/CNPJ) é imutável (identidade do tenant); razão social, nome
    /// fantasia, e-mail e status são editáveis.
    /// </summary>
    public void Update(string legalName, string tradeName, Email email, TenantStatus status)
    {
        SetLegalName(legalName);
        SetTradeName(tradeName);
        Email = email;
        Status = status;
    }

    /// <summary>
    /// Soft delete: inativa o tenant e registra quando. Tenants inativados ficam
    /// fora das consultas (query filter) e seus usuários não podem mais autenticar.
    /// </summary>
    public void SoftDelete()
    {
        IsActive = false;
        DeletedAtUtc ??= DateTime.UtcNow;
    }

    public Branch AddBranch(string name, Address address, Contact contact)
    {
        var branch = Branch.Create(Id, name, address, contact);
        _branches.Add(branch);
        return branch;
    }

    public void RemoveBranch(Branch branch)
    {
        if (!_branches.Remove(branch))
            throw new InvalidOperationException("A filial não pertence a este tenant.");
    }
}