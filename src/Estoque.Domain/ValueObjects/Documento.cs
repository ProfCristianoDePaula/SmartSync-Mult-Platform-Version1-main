using Estoque.Domain.Common;
using Estoque.Domain.Enums;

namespace Estoque.Domain.ValueObjects;

/// <summary>
/// Documento de fornecedor: CPF (pessoa física) ou CNPJ (pessoa jurídica).
/// Reutiliza os VOs Cpf/Cnpj — mesmo padrão do Tenant do Identity (Etapa 17).
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

    public static Documento Create(int? tipoPessoa, string? value)
    {
        if (!Enum.IsDefined(typeof(TipoPessoa), tipoPessoa ?? 0))
            throw new ArgumentException("Informe o tipo de pessoa (1 = Física, 2 = Jurídica).", nameof(tipoPessoa));

        var tipo = (TipoPessoa)tipoPessoa!;

        return tipo switch
        {
            TipoPessoa.Fisica => new Documento(tipo, Cpf.Create(value).Number),
            TipoPessoa.Juridica => new Documento(tipo, Cnpj.Create(value).Number),
            _ => throw new ArgumentException("Tipo de pessoa inválido.", nameof(tipoPessoa))
        };
    }

    /// <summary>Reconstrói a partir de valores já validados (leitura do banco).</summary>
    public static Documento FromValidated(TipoPessoa tipo, string numero) => new(tipo, numero);

    protected override IEnumerable<object?> GetEqualityValues()
    {
        yield return Numero;
    }

    public override string ToString() => $"{Tipo}: {Numero}";
}
