namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — SefazEndpoint.</summary>
public readonly record struct SefazEndpointId(Guid Value)
{
    public static SefazEndpointId New() => new(Guid.NewGuid());
    public static SefazEndpointId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
