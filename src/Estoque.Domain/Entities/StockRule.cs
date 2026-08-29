using Estoque.Domain.Common;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Regra de reposição: mínimo e ponto de pedido. Escopo TENANT com override
/// opcional por filial (BranchId nulo = regra padrão para todas as filiais).
/// </summary>
public sealed class StockRule : Entity<StockRuleId>
{
    public TenantId TenantId { get; private set; }
    public ProductId ProductId { get; private set; }
    public BranchId? BranchId { get; private set; }

    public Quantity MinimumQuantity { get; private set; }
    public Quantity ReorderPoint { get; private set; }
    public int? LeadTimeDays { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private StockRule() { }

    private StockRule(StockRuleId id, TenantId tenantId, ProductId productId,
        BranchId? branchId, Quantity minimum, Quantity reorderPoint, int? leadTimeDays)
        : base(id)
    {
        if (reorderPoint.Value < minimum.Value)
            throw new BusinessRuleViolationException(
                "Ponto de pedido não pode ser menor que o estoque mínimo.");

        TenantId = tenantId;
        ProductId = productId;
        BranchId = branchId;
        MinimumQuantity = minimum;
        ReorderPoint = reorderPoint;
        LeadTimeDays = leadTimeDays is < 0 or > 365 ? null : leadTimeDays;
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Cria ou substitui a regra de (tenant, produto[, filial]) — upsert semântico.</summary>
    public static StockRule Create(TenantId tenantId, Guid productId, Guid? branchId,
        decimal minimumQuantity, decimal reorderPoint, int? leadTimeDays)
        => new(
            StockRuleId.New(), tenantId, Common.ProductId.From(productId),
            branchId is null ? null : Common.BranchId.From(branchId.Value),
            Quantity.Positive(minimumQuantity),
            Quantity.Positive(reorderPoint),
            leadTimeDays);

    public void Update(decimal minimumQuantity, decimal reorderPoint, int? leadTimeDays)
    {
        var minimum = Quantity.Positive(minimumQuantity);
        var reorder = Quantity.Positive(reorderPoint);
        if (reorder.Value < minimum.Value)
            throw new BusinessRuleViolationException(
                "Ponto de pedido não pode ser menor que o estoque mínimo.");

        MinimumQuantity = minimum;
        ReorderPoint = reorder;
        LeadTimeDays = leadTimeDays is < 0 or > 365 ? null : leadTimeDays;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

