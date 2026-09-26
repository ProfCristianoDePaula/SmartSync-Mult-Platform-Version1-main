namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — NfseMunicipioConfig.</summary>
public readonly record struct NfseMunicipioConfigId(Guid Value)
{
    public static NfseMunicipioConfigId New() => new(Guid.NewGuid());
    public static NfseMunicipioConfigId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
