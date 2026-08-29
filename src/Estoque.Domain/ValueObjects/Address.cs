using Estoque.Domain.Common;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// Endereço (fornecedor).
/// </summary>
public sealed class Address : ValueObject
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string District { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }

    public Address(
        string street,
        string number,
        string? complement,
        string district,
        string city,
        string state,
        string postalCode)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Logradouro é obrigatório.", nameof(street));
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("Número é obrigatório.", nameof(number));
        if (string.IsNullOrWhiteSpace(district)) throw new ArgumentException("Bairro é obrigatório.", nameof(district));
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("Cidade é obrigatória.", nameof(city));

        if (state.Length != 2)
            throw new ArgumentException("Estado deve conter 2 letras (UF).", nameof(state));

        var postal = new string((postalCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (postal.Length != 8)
            throw new ArgumentException("CEP deve conter 8 dígitos.", nameof(postalCode));

        Street = street.Trim();
        Number = number.Trim();
        Complement = complement?.Trim();
        District = district.Trim();
        City = city.Trim();
        State = state.Trim().ToUpperInvariant();
        PostalCode = postal;
    }

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Street;
        yield return Number;
        yield return Complement;
        yield return District;
        yield return City;
        yield return State;
        yield return PostalCode;
    }

    public override string ToString()
        => $"{Street}, {Number}{(Complement is null ? string.Empty : " - " + Complement)} - {District}, {City}/{State} - CEP {PostalCode}";
}
