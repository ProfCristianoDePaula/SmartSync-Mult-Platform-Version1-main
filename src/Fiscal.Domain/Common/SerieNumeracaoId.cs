namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — SerieNumeracao.</summary>
public readonly record struct SerieNumeracaoId(Guid Value)
{
    public static SerieNumeracaoId New() => new(Guid.NewGuid());
    public static SerieNumeracaoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
