namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — UfFiscal.</summary>
public readonly record struct UfFiscalId(Guid Value)
{
    public static UfFiscalId New() => new(Guid.NewGuid());
    public static UfFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
