using Fiscal.Domain.Common;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Município: identidade é o código IBGE de 7 dígitos (nunca o nome).
/// Seed offline a partir da API de localidades do IBGE (Fiscal-3).
/// </summary>
public sealed class Municipio : Entity<MunicipioId>
{
    public string CodigoIbge { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public string Uf { get; private set; } = null!;

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private Municipio() { }

    private Municipio(MunicipioId id, string codigoIbge, string nome, string uf)
        : base(id)
    {
        SetCodigoIbge(codigoIbge);
        SetNome(nome);
        SetUf(uf);
        IsActive = true;
    }

    public static Municipio Create(string codigoIbge, string nome, string uf)
        => new(MunicipioId.New(), codigoIbge, nome, uf);

    public void Update(string nome, string uf)
    {
        SetNome(nome);
        SetUf(uf);
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetCodigoIbge(string codigoIbge)
    {
        var digits = new string((codigoIbge ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 7)
            throw new ArgumentException("Código IBGE deve conter 7 dígitos.", nameof(codigoIbge));
        CodigoIbge = digits;
    }

    private void SetNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome do município é obrigatório.", nameof(nome));
        if (nome.Trim().Length > 150)
            throw new ArgumentException("Nome do município excede 150 caracteres.", nameof(nome));
        Nome = nome.Trim();
    }

    private void SetUf(string uf)
    {
        if (string.IsNullOrWhiteSpace(uf) || uf.Trim().Length != 2)
            throw new ArgumentException("UF deve conter 2 letras.", nameof(uf));
        Uf = uf.Trim().ToUpperInvariant();
    }
}
