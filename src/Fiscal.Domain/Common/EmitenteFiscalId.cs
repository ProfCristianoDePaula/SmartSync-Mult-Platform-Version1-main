namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — EmitenteFiscal.</summary>
public readonly record struct EmitenteFiscalId(Guid Value)
{
    public static EmitenteFiscalId New() => new(Guid.NewGuid());
    public static EmitenteFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
