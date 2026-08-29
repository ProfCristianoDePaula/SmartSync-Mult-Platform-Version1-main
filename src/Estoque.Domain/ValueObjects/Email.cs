using Estoque.Domain.Common;
using System.Text.RegularExpressions;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// E-mail address, validated and normalized to lowercase at construction time.
/// </summary>
public sealed partial class Email : ValueObject
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("E-mail é obrigatório.", nameof(value));

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > 254)
            throw new ArgumentException("E-mail excede o tamanho máximo de 254 caracteres.", nameof(value));

        if (!EmailRegex().IsMatch(normalized))
            throw new ArgumentException("E-mail inválido.", nameof(value));

        return new Email(normalized);
    }

    /// <summary>Reconstrói a partir de valor já validado/normalizado (leitura do banco).</summary>
    public static Email FromValidated(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
