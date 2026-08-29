using Estoque.Domain.Common;
using Estoque.Domain.Enums;
using Estoque.Domain.Events;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Sugestão de compra (AutoCompra) — gerada pela política de reposição quando
/// o saldo atinge o ponto de pedido. Uma única sugestão ABERTA por
/// (tenant, produto, filial).
/// </summary>
public sealed class PurchaseSuggestion : Entity<PurchaseSuggestionId>, IHasDomainEvents
{
    private readonly List<object> _events = [];

    public TenantId TenantId { get; private set; }
    public ProductId ProductId { get; private set; }
    public BranchId BranchId { get; private set; }
    public SupplierId? SupplierId { get; private set; }

    public Quantity SuggestedQuantity { get; private set; }
    /// <summary>Saldo observado quando a sugestão foi gerada (contexto da decisão).</summary>
    public Quantity ObservedBalance { get; private set; }
    public PurchaseSuggestionStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }

    public IReadOnlyList<object> DomainEvents => _events;

    private PurchaseSuggestion() { }

    private PurchaseSuggestion(PurchaseSuggestionId id, TenantId tenantId, ProductId productId,
        Guid branchId, SupplierId? supplierId, Quantity suggested, Quantity observed)
        : base(id)
    {
        TenantId = tenantId;
        ProductId = productId;
        BranchId = Common.BranchId.From(branchId);
        SupplierId = supplierId;
        SuggestedQuantity = suggested;
        ObservedBalance = observed;
        Status = PurchaseSuggestionStatus.Aberta;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static PurchaseSuggestion Create(TenantId tenantId, Guid productId,
        Guid branchId, Guid? supplierId, decimal suggestedQuantity, decimal observedBalance)
    {
        var suggestion = new PurchaseSuggestion(
            PurchaseSuggestionId.New(), tenantId, Common.ProductId.From(productId), branchId,
            supplierId is null ? null : Common.SupplierId.From(supplierId.Value),
            Quantity.Positive(suggestedQuantity), Quantity.FromValidated(observedBalance));

        suggestion.Raise(new PurchaseSuggestionCreated(
            suggestion.Id, tenantId.Value, productId));

        return suggestion;
    }

    public void Approve(Guid decidedByUserId) => Decide(PurchaseSuggestionStatus.Aprovada, decidedByUserId);
    public void Discard(Guid decidedByUserId) => Decide(PurchaseSuggestionStatus.Descartada, decidedByUserId);

    private void Decide(PurchaseSuggestionStatus status, Guid decidedByUserId)
    {
        if (Status != PurchaseSuggestionStatus.Aberta)
            throw new BusinessRuleViolationException(
                "Somente sugestões ABERTAS podem ser aprovadas/descartadas.");

        Status = status;
        DecidedByUserId = decidedByUserId;
        DecidedAtUtc = DateTime.UtcNow;
    }

    public void Raise(object domainEvent) => _events.Add(domainEvent);
    public IReadOnlyList<object> PopEvents()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}

