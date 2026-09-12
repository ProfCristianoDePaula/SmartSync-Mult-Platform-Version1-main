using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Xunit;

namespace Estoque.Tests;

/// <summary>
/// Testes unitários das regras de cupom (Etapa 3) — cobrem os 3 cenários de prioridade sem depender de DB.
/// </summary>
public sealed class CupomPrioridadeTests
{
    private const decimal PrecoMock = 100m;

    private static decimal Calcular(Cupom cupom, IReadOnlyList<(Guid ProdutoId, Guid? CategoriaId, decimal Quantidade)> itens, IReadOnlyList<CupomProduto> vinculos)
    {
        HashSet<Guid>? elegiveis = vinculos.Count > 0 ? vinculos.Select(v => v.IdProduto.Value).ToHashSet() : null;
        decimal subtotal = 0;
        foreach (var item in itens)
        {
            bool ok;
            if (elegiveis is not null) ok = elegiveis.Contains(item.ProdutoId);
            else if (cupom.IdCategoria is not null) ok = item.CategoriaId == cupom.IdCategoria.Value.Value;
            else ok = true;
            if (ok) subtotal += item.Quantidade * PrecoMock;
        }
        if (subtotal < cupom.ValorMinimoCompra) throw new BusinessRuleViolationException("Valor mínimo não atingido.");
        if (cupom.ValorDesconto > 0) return Math.Min(cupom.ValorDesconto, subtotal);
        return Math.Round(subtotal * cupom.PercDesconto / 100m, 2);
    }

    [Fact]
    public void Cupom_Global_AplicaATodosOsProdutos()
    {
        var tenant = TenantId.New();
        var cupom = Cupom.Create(tenant, "Global 10%", 0, 10, 0, null, DateTime.UtcNow.AddDays(10), 10, false);
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var itens = new List<(Guid, Guid?, decimal)> { (p1, Guid.NewGuid(), 1), (p2, Guid.NewGuid(), 2) }; // 3 unidades *100 =300
        var desconto = Calcular(cupom, itens, []);
        Assert.Equal(30m, desconto); // 10% de 300
    }

    [Fact]
    public void Cupom_PorCategoria_AplicaSomenteProdutosDaCategoria()
    {
        var tenant = TenantId.New();
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        var cupom = Cupom.Create(tenant, "Cat A 20%", 0, 20, 0, catA, DateTime.UtcNow.AddDays(10), 10, false);
        var p1 = Guid.NewGuid(); // catA
        var p2 = Guid.NewGuid(); // catB
        var itens = new List<(Guid, Guid?, decimal)> { (p1, catA, 1), (p2, catB, 1) }; // subtotal elegível =100
        var desconto = Calcular(cupom, itens, []);
        Assert.Equal(20m, desconto); // 20% de 100
    }

    [Fact]
    public void Cupom_PorProduto_AplicaSomenteProdutosListados()
    {
        var tenant = TenantId.New();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var cupom = Cupom.Create(tenant, "Produto R$50", 50, 0, 0, null, DateTime.UtcNow.AddDays(10), 10, true);
        var vinculos = new List<CupomProduto> { CupomProduto.Create(cupom.Id, ProductId.From(p1)) };
        var itens = new List<(Guid, Guid?, decimal)> { (p1, null, 1), (p2, null, 1) }; // só p1 elegível =>100
        var desconto = Calcular(cupom, itens, vinculos);
        Assert.Equal(50m, desconto);
    }

    [Fact]
    public void Cupom_ValorDesconto_Xor_PercDesconto_Validacao()
    {
        var tenant = TenantId.New();
        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            Cupom.Create(tenant, "Inválido", 10, 10, 0, null, DateTime.UtcNow.AddDays(5), 5, false));
        Assert.Contains("apenas ValorDesconto OU PercDesconto", ex.Message);
    }

    [Fact]
    public void Cupom_ValidadeRetroativa_Bloqueada()
    {
        var tenant = TenantId.New();
        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            Cupom.Create(tenant, "Retro", 10, 0, 0, null, DateTime.UtcNow.AddDays(-1), 5, false));
        Assert.Contains("retroativa", ex.Message);
    }

    [Fact]
    public void Pedido_IdCarrinho_Alias_Equivalente()
    {
        var pedidoId = PedidoId.New();
        var item = ProdutosPedido.Create(pedidoId, ProductId.New(), 2);
        Assert.Equal(item.IdPedido, item.IdCarrinho);
        Assert.Equal(pedidoId, item.IdPedido);
    }

    [Fact]
    public void Checkout_Parcelas_Pix_SoPermiteUma()
    {
        var formaPix = FormaPagto.Create("Pix", 1);
        Assert.False(formaPix.EhParcelavel);
        Assert.Equal(1, formaPix.QtdMaximaParcelas);
        var formaCredito = FormaPagto.Create("Cartão de Crédito", 12);
        Assert.True(formaCredito.EhParcelavel);
    }

    [Fact]
    public void Venda_Cria_ComValoresCalculados()
    {
        var pedidoId = PedidoId.New();
        var tenant = TenantId.New();
        var formaId = FormaPagtoId.New();
        var venda = Venda.Criar(pedidoId, tenant, formaId, 300m, 30m, 15m, false, 3);
        Assert.Equal(270m, venda.ValorLiquidoPedido);
        Assert.Equal(285m, venda.ValorFinal);
    }
}
