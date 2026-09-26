namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — AlertaFiscal.</summary>
public readonly record struct AlertaFiscalId(Guid Value)
{
    public static AlertaFiscalId New() => new(Guid.NewGuid());
    public static AlertaFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
