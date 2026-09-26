namespace Fiscal.Domain.Common;

/// <summary>Identificador tipado — ProdutoFiscal.</summary>
public readonly record struct ProdutoFiscalId(Guid Value)
{
    public static ProdutoFiscalId New() => new(Guid.NewGuid());
    public static ProdutoFiscalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
