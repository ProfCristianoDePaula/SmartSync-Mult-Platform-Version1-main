namespace Estoque.Domain.Common;

/// <summary>Identificador tipado — CupomProduto (vínculo cupom↔produto).</summary>
public readonly record struct CupomProdutoId(Guid Value)
{
    public static CupomProdutoId New() => new(Guid.NewGuid());
    public static CupomProdutoId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
