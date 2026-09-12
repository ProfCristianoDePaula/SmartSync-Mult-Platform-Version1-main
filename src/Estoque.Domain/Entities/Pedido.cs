using Estoque.Domain.Common;
using Estoque.Domain.Enums;

namespace Estoque.Domain.Entities;

/// <summary>
/// Pedido / Carrinho — agregado raiz do fluxo de compra. Status Aberto = carrinho
/// em edição. Isola por TenantId (multi-tenancy) + IdUnidade/BranchId (filial).
/// ValorTotal é recalculado pelos serviços de carrinho/cupom.
/// </summary>
public sealed class Pedido : Entity<PedidoId>, IHasDomainEvents
{
    private readonly List<object> _events = [];
    private readonly List<ProdutosPedido> _itens = [];

    public TenantId TenantId { get; private set; }
    public DateTime DataAbertura { get; private set; }
    public Guid? IdCliente { get; private set; }
    public CupomId? IdCupom { get; private set; }
    public BranchId? IdUnidade { get; private set; }
    public PedidoStatus Status { get; private set; }
    public DateTime? DataFechamento { get; private set; }
    public decimal ValorTotal { get; private set; }

    public IReadOnlyList<ProdutosPedido> Itens => _itens.AsReadOnly();
    public IReadOnlyList<object> DomainEvents => _events;

    private Pedido() { }

    private Pedido(
        PedidoId id,
        TenantId tenantId,
        DateTime dataAbertura,
        Guid? idCliente,
        CupomId? idCupom,
        BranchId? idUnidade,
        PedidoStatus status,
        DateTime? dataFechamento,
        decimal valorTotal)
        : base(id)
    {
        TenantId = tenantId;
        DataAbertura = dataAbertura;
        IdCliente = idCliente;
        IdCupom = idCupom;
        IdUnidade = idUnidade;
        Status = status;
        DataFechamento = dataFechamento;
        ValorTotal = Math.Round(valorTotal, 2, MidpointRounding.ToEven);
    }

    public static Pedido CriarCarrinho(TenantId tenantId, Guid? idCliente, BranchId? idUnidade)
        => new(
            PedidoId.New(),
            tenantId,
            DateTime.UtcNow,
            idCliente,
            null,
            idUnidade,
            PedidoStatus.Aberto,
            null,
            0m);

    public static Pedido FromValidated(
        PedidoId id,
        TenantId tenantId,
        DateTime dataAbertura,
        Guid? idCliente,
        CupomId? idCupom,
        BranchId? idUnidade,
        PedidoStatus status,
        DateTime? dataFechamento,
        decimal valorTotal)
        => new(id, tenantId, dataAbertura, idCliente, idCupom, idUnidade, status, dataFechamento, valorTotal);

    public void AplicarCupom(CupomId? idCupom)
    {
        if (Status != PedidoStatus.Aberto)
            throw new BusinessRuleViolationException("Cupom só pode ser aplicado em pedido com status Aberto.");
        IdCupom = idCupom;
    }

    public void RemoverCupom() => IdCupom = null;

    public void AtualizarValorTotal(decimal novoTotal)
    {
        if (novoTotal < 0)
            throw new ArgumentException("Valor total não pode ser negativo.", nameof(novoTotal));
        ValorTotal = Math.Round(novoTotal, 2, MidpointRounding.ToEven);
    }

    public void Fechar(PedidoStatus statusFechamento = PedidoStatus.VendaEfetuada)
    {
        if (Status != PedidoStatus.Aberto && Status != PedidoStatus.PagamentoAprovado && Status != PedidoStatus.AguardandoPagamento)
            throw new BusinessRuleViolationException($"Pedido não pode ser fechado a partir do status {Status}.");
        if (statusFechamento != PedidoStatus.VendaEfetuada && statusFechamento != PedidoStatus.Cancelado)
            throw new ArgumentException("Status de fechamento deve ser VendaEfetuada ou Cancelado.", nameof(statusFechamento));

        Status = statusFechamento;
        DataFechamento = DateTime.UtcNow;
    }

    public void AlterarStatus(PedidoStatus novoStatus)
    {
        Status = novoStatus;
        if (novoStatus is PedidoStatus.VendaEfetuada or PedidoStatus.Cancelado)
            DataFechamento ??= DateTime.UtcNow;
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}
