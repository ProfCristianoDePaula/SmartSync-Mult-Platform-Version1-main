namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — DocumentoFiscal.</summary>
public readonly record struct DocumentoFiscalId(Guid Value)
{
    public static DocumentoFiscalId New() => new(Guid.NewGuid());
    public static DocumentoFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
