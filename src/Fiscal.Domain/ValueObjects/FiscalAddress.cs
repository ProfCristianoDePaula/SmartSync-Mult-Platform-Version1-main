using Fiscal.Domain.Common;

namespace Fiscal.Domain.ValueObjects;

/// <summary>
/// Endereço fiscal do emitente: endereço operacional + código IBGE do
/// município (nunca o nome da cidade como chave — R3).
/// </summary>
public sealed class FiscalAddress : ValueObject
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string District { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string CodigoIbgeMunicipio { get; }

    public FiscalAddress(
        string street, string number, string? complement, string district,
        string city, string state, string postalCode, string codigoIbgeMunicipio)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Logradouro é obrigatório.", nameof(street));
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Número é obrigatório.", nameof(number));
        if (string.IsNullOrWhiteSpace(district)) throw new ArgumentException("Bairro é obrigatório.", nameof(district));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("Cidade é obrigatória.", nameof(city));
        if (state.Trim().Length != 2) throw new ArgumentException("UF deve conter 2 letras.", nameof(state));

        var postal = new string(postalCode.Where(char.IsDigit).ToArray());
        if (postal.Length != 8) throw new ArgumentException("CEP deve conter 8 dígitos.", nameof(postalCode));

        var ibge = new string(codigoIbgeMunicipio.Where(char.IsDigit).ToArray());
        if (ibge.Length != 7) throw new ArgumentException("Código IBGE do município deve conter 7 dígitos.", nameof(codigoIbgeMunicipio));

        Street = street.Trim();
        Number = number.Trim();
        Complement = complement?.Trim();
        District = district.Trim();
        City = city.Trim();
        State = state.Trim().ToUpperInvariant();
        PostalCode = postal;
        CodigoIbgeMunicipio = ibge;
    }

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Street; yield return Number; yield return Complement;
        yield return District; yield return City; yield return State;
        yield return PostalCode; yield return CodigoIbgeMunicipio;
    }
}
