namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — ConcessaoUnidade.</summary>
public readonly record struct ConcessaoUnidadeId(Guid Value)
{
    public static ConcessaoUnidadeId New() => new(Guid.NewGuid());
    public static ConcessaoUnidadeId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
