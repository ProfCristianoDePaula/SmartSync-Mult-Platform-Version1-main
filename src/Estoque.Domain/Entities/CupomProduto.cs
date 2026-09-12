using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>
/// Vínculo N:N entre Cupom e Produto — quando existem registros aqui, o cupom
/// é exclusivo para os produtos listados (prioridade 1 na aplicação).
/// </summary>
public sealed class CupomProduto : Entity<CupomProdutoId>
{
    public CupomId IdCupom { get; private set; }
    public ProductId IdProduto { get; private set; }

    private CupomProduto() { }

    private CupomProduto(CupomProdutoId id, CupomId idCupom, ProductId idProduto)
        : base(id)
    {
        IdCupom = idCupom;
        IdProduto = idProduto;
    }

    public static CupomProduto Create(CupomId idCupom, ProductId idProduto)
        => new(CupomProdutoId.New(), idCupom, idProduto);

    public static CupomProduto FromIds(Guid idCupom, Guid idProduto)
        => new(CupomProdutoId.New(), CupomId.From(idCupom), ProductId.From(idProduto));
}
