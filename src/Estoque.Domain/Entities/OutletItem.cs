using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Estoque.Domain.Events;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Item marcado como OUTLET (avaria/devolução/vencimento próximo) em uma
/// filial — reduz o saldo vendável daquele produto naquela filial.
/// </summary>
public sealed class OutletItem : Entity<OutletItemId>, IHasDomainEvents
{
    private readonly List<object> _events = [];

    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public ProductId ProductId { get; private set; }

    public OutletReason Reason { get; private set; }
    /// <summary>Desconto sugerido (0–90%).</summary>
    public int SuggestedDiscountPct { get; private set; }
    public Quantity Quantity { get; private set; }

    /// <summary>Usuário que marcou (claim user_id).</summary>
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    public IReadOnlyList<object> DomainEvents => _events;

    private OutletItem() { }

    private OutletItem(OutletItemId id, TenantId tenantId, Guid branchId, ProductId productId,
        OutletReason reason, int discountPct, Quantity quantity, Guid createdByUserId)
        : base(id)
    {
        if (discountPct is < 0 or > 90)
            throw new BusinessRuleViolationException("Desconto sugerido deve estar entre 0% e 90%.");

        TenantId = tenantId;
        BranchId = Common.BranchId.From(branchId);
        ProductId = productId;
        Reason = reason;
        SuggestedDiscountPct = discountPct;
        Quantity = quantity;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static OutletItem Create(TenantId tenantId, Guid branchId, Guid productId,
        OutletReason reason, decimal quantity, int discountPct, Guid createdByUserId)
    {
        var outlet = new OutletItem(
            OutletItemId.New(), tenantId, branchId, Common.ProductId.From(productId),
            reason, discountPct, Quantity.Positive(quantity), createdByUserId);

        outlet.Raise(new OutletMarked(
            outlet.Id, tenantId.Value, branchId, productId, reason.ToString()));

        return outlet;
    }

    /// <summary>Resolve (baixa) o item de outlet.</summary>
    public void Resolve()
    {
        if (ResolvedAtUtc is not null)
            return;
        ResolvedAtUtc = DateTime.UtcNow;
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}

