namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — Item da venda (ProdutosVenda).</summary>
public readonly record struct ProdutosVendaId(Guid Value)
{
    public static ProdutosVendaId New() => new(Guid.NewGuid());
    public static ProdutosVendaId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
