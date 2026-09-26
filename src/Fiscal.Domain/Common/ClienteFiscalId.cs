namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — ClienteFiscal.</summary>
public readonly record struct ClienteFiscalId(Guid Value)
{
    public static ClienteFiscalId New() => new(Guid.NewGuid());
    public static ClienteFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
