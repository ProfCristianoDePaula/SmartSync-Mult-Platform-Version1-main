namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — NfseAmbiente.</summary>
public readonly record struct NfseAmbienteId(Guid Value)
{
    public static NfseAmbienteId New() => new(Guid.NewGuid());
    public static NfseAmbienteId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
