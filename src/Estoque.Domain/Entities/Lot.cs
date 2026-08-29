using Estoque.Domain.Common;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Entities;

/// <summary>
/// Lote com validade — quantidade rastreada por (produto, filial, número).
/// A data de validade alimenta alertas e sugestões de outlet.
/// </summary>
public sealed class Lot : Entity<LotId>
{
    public TenantId TenantId { get; private set; }
    public BranchId BranchId { get; private set; }
    public ProductId ProductId { get; private set; }
    public SupplierId? SupplierId { get; private set; }

    public string Number { get; private set; } = null!;
    public DateOnly ExpiresOn { get; private set; }
    public Quantity Quantity { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Lot() { }

    private Lot(LotId id, TenantId tenantId, Guid branchId, ProductId productId,
        SupplierId? supplierId, string number, DateOnly expiresOn, Quantity quantity)
        : base(id)
    {
        if (expiresOn < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new BusinessRuleViolationException(
                "Não é possível cadastrar um lote já vencido.");

        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Número do lote é obrigatório.", nameof(number));

        TenantId = tenantId;
        BranchId = Common.BranchId.From(branchId);
        ProductId = productId;
        SupplierId = supplierId;
        Number = number.Trim();
        ExpiresOn = expiresOn;
        Quantity = quantity;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Lot Create(TenantId tenantId, Guid branchId, Guid productId,
        Guid? supplierId, string number, DateOnly expiresOn, decimal quantity)
        => new(
            LotId.New(), tenantId, branchId, Common.ProductId.From(productId),
            supplierId is null ? null : Common.SupplierId.From(supplierId.Value),
            number, expiresOn, Quantity.Positive(quantity));

    /// <summary>Reconstrói lotes legados (data no passado permitida — leitura/migração).</summary>
    public static Lot FromValidated(TenantId tenantId, Guid branchId, Guid productId,
        Guid? supplierId, string number, DateOnly expiresOn, decimal quantity)
        => new(LotId.New(), tenantId, branchId, Common.ProductId.From(productId),
               supplierId is null ? null : Common.SupplierId.From(supplierId.Value),
               number, expiresOn, Quantity.Positive(quantity));

    public void Add(Quantity quantity) => Quantity += quantity;

    public void Consume(Quantity quantity)
    {
        if (quantity.Value > Quantity.Value)
            throw new BusinessRuleViolationException(
                $"Quantidade insuficiente no lote {Number}: disponível {Quantity}.");
        Quantity -= quantity;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }
}

