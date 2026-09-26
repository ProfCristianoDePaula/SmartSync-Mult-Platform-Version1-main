using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Estabelecimento emitente: filial (Branch) com identidade fiscal própria
/// (CNPJ/IE/IM/CNAE/CRT + endereço com IBGE). Unidade operacional ≠
/// estabelecimento até este vínculo existir — emissão sem emitente é
/// bloqueada. Filiais do mesmo CNPJ-base compartilham certificado
/// (referência no tenant ou override na filial — Fiscal-5).
/// </summary>
public sealed class EmitenteFiscal : Entity<EmitenteFiscalId>
{
    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public CnpjFiscal Cnpj { get; private set; } = null!;
    public string RazaoSocial { get; private set; } = null!;
    public string Fantasia { get; private set; } = null!;
    public string? InscricaoEstadual { get; private set; }
    public string? InscricaoMunicipal { get; private set; }
    public string? Cnae { get; private set; }
    public CrtFiscal Crt { get; private set; }
    public FiscalAddress Endereco { get; private set; } = null!;
    public string Telefone { get; private set; } = null!;
    public string Email { get; private set; } = null!;

    public bool EmissaoAutomaticaVenda { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private EmitenteFiscal() { }

    private EmitenteFiscal(
        EmitenteFiscalId id, TenantId tenantId, BranchId branchId, CnpjFiscal cnpj,
        string razaoSocial, string fantasia, string? inscricaoEstadual,
        string? inscricaoMunicipal, string? cnae, CrtFiscal crt,
        FiscalAddress endereco, string telefone, string email)
        : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        Cnpj = cnpj;
        SetRazao(razaoSocial);
        SetFantasia(fantasia);
        InscricaoEstadual = NormSimples(inscricaoEstadual, 20);
        InscricaoMunicipal = NormSimples(inscricaoMunicipal, 20);
        Cnae = NormDigits(cnae, 7);
        Crt = crt;
        Endereco = endereco;
        SetTelefone(telefone);
        SetEmail(email);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static EmitenteFiscal Create(
        TenantId tenantId, BranchId branchId, string cnpj,
        string razaoSocial, string fantasia, string? inscricaoEstadual,
        string? inscricaoMunicipal, string? cnae, CrtFiscal crt,
        FiscalAddress endereco, string telefone, string email)
        => new(EmitenteFiscalId.New(), tenantId, branchId, CnpjFiscal.Create(cnpj),
            razaoSocial, fantasia, inscricaoEstadual, inscricaoMunicipal,
            cnae, crt, endereco, telefone, email);

    public void Update(
        string razaoSocial, string fantasia, string? inscricaoEstadual,
        string? inscricaoMunicipal, string? cnae, CrtFiscal crt,
        FiscalAddress endereco, string telefone, string email)
    {
        SetRazao(razaoSocial);
        SetFantasia(fantasia);
        InscricaoEstadual = NormSimples(inscricaoEstadual, 20);
        InscricaoMunicipal = NormSimples(inscricaoMunicipal, 20);
        Cnae = NormDigits(cnae, 7);
        Crt = crt;
        Endereco = endereco;
        SetTelefone(telefone);
        SetEmail(email);
    }

    public void DefinirEmissaoAutomatica(bool ativa) => EmissaoAutomaticaVenda = ativa;

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetRazao(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException("Razão social é obrigatória.", nameof(v));
        RazaoSocial = v.Trim();
    }

    private void SetFantasia(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException("Nome fantasia é obrigatório.", nameof(v));
        Fantasia = v.Trim();
    }

    private void SetTelefone(string v)
    {
        var d = new string((v ?? "").Where(char.IsDigit).ToArray());
        if (d.Length is not (10 or 11)) throw new ArgumentException("Telefone deve conter 10 ou 11 dígitos.", nameof(v));
        Telefone = d;
    }

    private void SetEmail(string v)
    {
        if (string.IsNullOrWhiteSpace(v) || !v.Contains('@')) throw new ArgumentException("E-mail inválido.", nameof(v));
        Email = v.Trim();
    }

    private static string? NormSimples(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (v.Trim().Length > max) throw new ArgumentException($"Campo excede {max} caracteres.");
        return v.Trim();
    }

    private static string? NormDigits(string? v, int len)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var d = new string(v.Where(char.IsDigit).ToArray());
        if (d.Length != len) throw new ArgumentException($"Campo deve conter {len} dígitos.");
        return d;
    }
}
