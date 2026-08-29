using Estoque.Domain.Common;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Saldo de um produto em uma filial — identidade natural
/// (tenant, produto, filial). NUNCA é alterado diretamente: apenas o
/// <c>MovementApplier</c> (serviço de domínio) o atualiza ao aplicar
/// movimentações.
/// </summary>
public sealed class StockBalance : Entity<Guid>, IHasDomainEvents
{
    private readonly List<object> _events = [];

    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public ProductId ProductId { get; private set; }

    public Quantity Quantity { get; private set; }
    public Money? AverageUnitCost { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<object> DomainEvents => _events;

    private StockBalance() { }

    public StockBalance(TenantId tenantId, Guid branchId, ProductId productId)
        : base(Guid.NewGuid())
    {
        TenantId = tenantId;
        BranchId = Common.BranchId.From(branchId);
        ProductId = productId;
        Quantity = Quantity.FromValidated(0);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Entrada: soma quantidade e recalcula custo médio ponderado.</summary>
    public void ApplyIn(Quantity quantity, Money? unitCost)
    {
        if (unitCost is not null && Quantity.Value > 0 && AverageUnitCost is not null)
        {
            var total = Quantity.Value + quantity.Value;
            var weighted =
                (Quantity.Value * AverageUnitCost.Value.Amount + quantity.Value * unitCost.Value.Amount) / total;
            AverageUnitCost = Money.Create(Math.Round(weighted, 2, MidpointRounding.ToEven));
        }
        else if (unitCost is not null)
        {
            AverageUnitCost = unitCost;
        }

        Quantity += quantity;
        Touch();
    }

    /// <summary>Saída: subtrai quantidade — invariante saldo ≥ 0.</summary>
    public void ApplyOut(Quantity quantity)
    {
        if (quantity.Value > Quantity.Value)
            throw new BusinessRuleViolationException(
                $"Saldo insuficiente: disponível {Quantity}, solicitado {quantity}.");
        Quantity -= quantity;
        Touch();
    }

    /// <summary>Ajuste: delta pode ser negativo (inventário), mas não pode deixar saldo negativo.</summary>
    public void ApplyAdjustment(decimal delta)
    {
        var target = Quantity.Value + delta;
        if (target < 0)
            throw new BusinessRuleViolationException(
                "Ajuste deixaria o saldo negativo.");
        Quantity = Quantity.FromValidated(target);
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}

