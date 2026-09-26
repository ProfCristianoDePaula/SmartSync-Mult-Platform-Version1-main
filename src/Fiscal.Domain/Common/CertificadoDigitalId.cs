namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — CertificadoDigital.</summary>
public readonly record struct CertificadoDigitalId(Guid Value)
{
    public static CertificadoDigitalId New() => new(Guid.NewGuid());
    public static CertificadoDigitalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
