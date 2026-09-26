namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — ConfiguracaoDocumento.</summary>
public readonly record struct ConfiguracaoDocumentoId(Guid Value)
{
    public static ConfiguracaoDocumentoId New() => new(Guid.NewGuid());
    public static ConfiguracaoDocumentoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
