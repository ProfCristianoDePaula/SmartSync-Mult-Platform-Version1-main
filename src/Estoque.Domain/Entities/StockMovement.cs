using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Estoque.Domain.Events;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Movimentação de estoque — log APPEND-ONLY (nunca editada/removida).
/// Carimba o usuário autenticado (claim user_id do JWT do Identity) para
/// auditoria. Transferência gera par espelhado na mesma transação.
/// </summary>
public sealed class StockMovement : Entity<StockMovementId>, IHasDomainEvents
{
    private readonly List<object> _events = [];

    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public ProductId ProductId { get; private set; }
    public MovementType Type { get; private set; }
    public Quantity Quantity { get; private set; }
    public Money? UnitCost { get; private set; }
    public LotId? LotId { get; private set; }
    public string? OriginDocumentRef { get; private set; }

    /// <summary>Usuário que executou a operação — claim user_id (Identity).</summary>
    public Guid PerformedByUserId { get; private set; }

    /// <summary>Filial parceira nas transferências (origem ou destino).</summary>
    public Guid? CounterpartyBranchId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<object> DomainEvents => _events;

    private StockMovement() { }

    // Tipos qualificados: propriedades homônimas (BranchId, LotId...) sombreiam
    // os tipos dentro da classe.
    private StockMovement(
        StockMovementId id, TenantId tenantId, Guid branchId, ProductId productId,
        MovementType type, Quantity quantity, Money? unitCost, LotId? lotId,
        string? originDocumentRef, Guid performedByUserId, Guid? counterpartyBranchId)
        : base(id)
    {
        TenantId = tenantId;
        BranchId = Common.BranchId.From(branchId);
        ProductId = Common.ProductId.From(productId.Value);
        Type = type;
        Quantity = quantity;
        UnitCost = unitCost;
        LotId = lotId;
        OriginDocumentRef = originDocumentRef?.Trim();
        PerformedByUserId = performedByUserId;
        CounterpartyBranchId = counterpartyBranchId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    internal static StockMovement Append(
        TenantId tenantId,
        Guid branchId,
        ProductId productId,
        MovementType type,
        Quantity quantity,
        Money? unitCost,
        LotId? lotId,
        string? originDocumentRef,
        Guid performedByUserId,
        Guid? counterpartyBranchId)
    {
        var movement = new StockMovement(
            StockMovementId.New(), tenantId, branchId, productId, type,
            quantity, unitCost, lotId, originDocumentRef, performedByUserId, counterpartyBranchId);

        movement.Raise(new StockMovementRegistered(
            movement.Id.Value, tenantId.Value, branchId, productId.Value, type, quantity.Value));

        return movement;
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}
