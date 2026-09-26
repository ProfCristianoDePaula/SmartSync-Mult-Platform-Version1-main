using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// UF do catálogo global (SuperAdmin). Sigla + cUF IBGE + nome + autorizadores
/// de NF-e/NFC-e. É a fonte que o admin do tenant/unidade usa para escolher
/// UF e ambientes — nunca contém URLs (estas vivem em <see cref="SefazEndpoint"/>).
/// </summary>
public sealed class UfFiscal : Entity<UfFiscalId>
{
    public string Sigla { get; private set; } = null!;
    public int CodigoIbge { get; private set; }
    public string Nome { get; private set; } = null!;
    public AutorizadorTipo AutorizadorNFe { get; private set; }
    public AutorizadorTipo AutorizadorNFCe { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private UfFiscal() { }

    private UfFiscal(
        UfFiscalId id,
        string sigla,
        int codigoIbge,
        string nome,
        AutorizadorTipo autorizadorNFe,
        AutorizadorTipo autorizadorNFCe)
        : base(id)
    {
        SetSigla(sigla);
        SetCodigoIbge(codigoIbge);
        SetNome(nome);
        AutorizadorNFe = autorizadorNFe;
        AutorizadorNFCe = autorizadorNFCe;
        IsActive = true;
    }

    public static UfFiscal Create(
        string sigla,
        int codigoIbge,
        string nome,
        AutorizadorTipo autorizadorNFe,
        AutorizadorTipo autorizadorNFCe)
        => new(UfFiscalId.New(), sigla, codigoIbge, nome, autorizadorNFe, autorizadorNFCe);

    public void Update(
        int codigoIbge,
        string nome,
        AutorizadorTipo autorizadorNFe,
        AutorizadorTipo autorizadorNFCe)
    {
        SetCodigoIbge(codigoIbge);
        SetNome(nome);
        AutorizadorNFe = autorizadorNFe;
        AutorizadorNFCe = autorizadorNFCe;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetSigla(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla) || sigla.Trim().Length != 2)
            throw new ArgumentException("Sigla da UF deve conter 2 letras.", nameof(sigla));
        Sigla = sigla.Trim().ToUpperInvariant();
    }

    private void SetCodigoIbge(int codigoIbge)
    {
        if (codigoIbge < 11 || codigoIbge > 53)
            throw new ArgumentException("Código IBGE da UF (cUF) inválido.", nameof(codigoIbge));
        CodigoIbge = codigoIbge;
    }

    private void SetNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome da UF é obrigatório.", nameof(nome));
        if (nome.Trim().Length > 100)
            throw new ArgumentException("Nome da UF excede 100 caracteres.", nameof(nome));
        Nome = nome.Trim();
    }
}
