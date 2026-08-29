using Identity.Domain.Common;
using Identity.Domain.Enums;

namespace Identity.Domain.ValueObjects;

/// <summary>
/// Documento de identificação de um tenant (Etapa 17): CPF (pessoa física)
/// ou CNPJ (pessoa jurídica). A validação é delegada ao VO correspondente no
/// momento da criação — um documento inválido nunca existe em memória.
/// </summary>
public sealed class Documento : ValueObject
{
    public TipoPessoa Tipo { get; }
    public string Numero { get; }

    private Documento(TipoPessoa tipo, string numero)
    {
        Tipo = tipo;
        Numero = numero;
    }

    public static Documento Create(TipoPessoa tipo, string? value)
    {
        return tipo switch
        {
            TipoPessoa.Fisica => new Documento(tipo, Cpf.Create(value).Number),
            TipoPessoa.Juridica => new Documento(tipo, Cnpj.Create(value).Number),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de pessoa inválido.")
        };
    }

    /// <summary>
    /// Rebuilds a document from an already-validated, normalized number
    /// (e.g. when loading from the database). Does not re-validate.
    /// </summary>
    public static Documento FromValidated(TipoPessoa tipo, string number)
        => new(tipo, number);

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Tipo;
        yield return Numero;
    }

    public override string ToString()
        => Numero;
}
