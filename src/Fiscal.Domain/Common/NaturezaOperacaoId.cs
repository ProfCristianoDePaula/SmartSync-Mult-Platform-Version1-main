namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — NaturezaOperacao.</summary>
public readonly record struct NaturezaOperacaoId(Guid Value)
{
    public static NaturezaOperacaoId New() => new(Guid.NewGuid());
    public static NaturezaOperacaoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
