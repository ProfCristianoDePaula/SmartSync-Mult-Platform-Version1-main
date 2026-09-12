using Estoque.Domain.Common;

namespace Estoque.Domain.Entities;

/// <summary>
/// Venda — fechamento do Pedido. Criada em transação atômica com ProdutosVenda
/// e atualização do Pedido (Etapa 5). Valores monetários em numeric(18,2).
/// </summary>
public sealed class Venda : Entity<VendaId>, IHasDomainEvents
{
    private readonly List<object> _events = [];
    private readonly List<ProdutosVenda> _itens = [];

    public PedidoId IdPedido { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTime DataVenda { get; private set; }
    public FormaPagtoId IdFormaPagto { get; private set; }

    public decimal ValorBruto { get; private set; }
    public decimal ValorDesconto { get; private set; }
    public decimal ValorLiquidoPedido { get; private set; }
    public decimal ValorFrete { get; private set; }
    public decimal ValorFinal { get; private set; }

    public bool IsPago { get; private set; }
    public string? NrPedido { get; private set; }
    public int QuantidadeParcelar { get; private set; }

    public IReadOnlyList<ProdutosVenda> Itens => _itens.AsReadOnly();
    public IReadOnlyList<object> DomainEvents => _events;

    private Venda() { }

    private Venda(
        VendaId id,
        PedidoId idPedido,
        TenantId tenantId,
        DateTime dataVenda,
        FormaPagtoId idFormaPagto,
        decimal valorBruto,
        decimal valorDesconto,
        decimal valorLiquidoPedido,
        decimal valorFrete,
        decimal valorFinal,
        bool isPago,
        string? nrPedido,
        int quantidadeParcelar)
        : base(id)
    {
        if (valorBruto < 0 || valorDesconto < 0 || valorLiquidoPedido < 0 || valorFrete < 0 || valorFinal < 0)
            throw new ArgumentException("Valores monetários não podem ser negativos.");
        if (quantidadeParcelar < 1)
            throw new ArgumentException("Quantidade de parcelas deve ser ao menos 1.", nameof(quantidadeParcelar));
        if (nrPedido is not null && nrPedido.Trim().Length > 50)
            throw new ArgumentException("NrPedido excede 50 caracteres.", nameof(nrPedido));

        IdPedido = idPedido;
        TenantId = tenantId;
        DataVenda = dataVenda;
        IdFormaPagto = idFormaPagto;
        ValorBruto = Math.Round(valorBruto, 2, MidpointRounding.ToEven);
        ValorDesconto = Math.Round(valorDesconto, 2, MidpointRounding.ToEven);
        ValorLiquidoPedido = Math.Round(valorLiquidoPedido, 2, MidpointRounding.ToEven);
        ValorFrete = Math.Round(valorFrete, 2, MidpointRounding.ToEven);
        ValorFinal = Math.Round(valorFinal, 2, MidpointRounding.ToEven);
        IsPago = isPago;
        NrPedido = string.IsNullOrWhiteSpace(nrPedido) ? null : nrPedido.Trim();
        QuantidadeParcelar = quantidadeParcelar;
    }

    public static Venda Criar(
        PedidoId idPedido,
        TenantId tenantId,
        FormaPagtoId idFormaPagto,
        decimal valorBruto,
        decimal valorDesconto,
        decimal valorFrete,
        bool isPago,
        int quantidadeParcelar,
        string? nrPedido = null)
    {
        var valorLiquido = Math.Round(valorBruto - valorDesconto, 2, MidpointRounding.ToEven);
        if (valorLiquido < 0) valorLiquido = 0;
        var valorFinal = Math.Round(valorLiquido + valorFrete, 2, MidpointRounding.ToEven);

        return new Venda(
            VendaId.New(),
            idPedido,
            tenantId,
            DateTime.UtcNow,
            idFormaPagto,
            valorBruto,
            valorDesconto,
            valorLiquido,
            valorFrete,
            valorFinal,
            isPago,
            nrPedido,
            quantidadeParcelar);
    }

    public void MarcarPago() => IsPago = true;
    public void DefinirNrPedido(string nrPedido)
    {
        if (string.IsNullOrWhiteSpace(nrPedido))
            throw new ArgumentException("NrPedido não pode ser vazio.", nameof(nrPedido));
        NrPedido = nrPedido.Trim();
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}
