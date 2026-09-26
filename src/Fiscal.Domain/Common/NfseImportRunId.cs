namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — NfseImportRun.</summary>
public readonly record struct NfseImportRunId(Guid Value)
{
    public static NfseImportRunId New() => new(Guid.NewGuid());
    public static NfseImportRunId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
