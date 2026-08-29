using Estoque.Domain.Common;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// Dados de contato (fornecedor).
/// </summary>
public sealed class Contact : ValueObject
{
    public string Phone { get; }
    public string? SecondaryPhone { get; }
    public Email Email { get; }

    public Contact(string phone, Email email, string? secondaryPhone = null)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.Length is < 10 or > 11)
            throw new ArgumentException("Telefone deve conter 10 (fixo) ou 11 (celular) dígitos.", nameof(phone));

        Phone = digits;
        Email = email ?? throw new ArgumentException("E-mail de contato é obrigatório.", nameof(email));

        var secondary = secondaryPhone is null
            ? null
            : new string(secondaryPhone.Where(char.IsDigit).ToArray());

        if (secondary is { Length: < 10 or > 11 })
            throw new ArgumentException("Telefone secundário inválido.", nameof(secondaryPhone));

        SecondaryPhone = secondary;
    }

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Phone;
        yield return SecondaryPhone;
        yield return Email;
    }
}
