using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Services;

/// <summary>
/// Serviço de domínio que aplica uma movimentação ao saldo, garantindo as
/// invariantes (saldo ≥ 0; custo médio ponderado) e produzindo a movimentação
/// append-only correspondente. Transacional — quem chama persiste saldo +
/// movimentação na MESMA transação.
/// </summary>
public static class MovementApplier
{
    public static StockMovement ApplyIn(
        StockBalance balance,
        TenantId tenantId,
        Guid branchId,
        ProductId productId,
        Quantity quantity,
        Money? unitCost,
        LotId? lotId,
        string? originDocumentRef,
        Guid performedByUserId)
    {
        balance.ApplyIn(quantity, unitCost);

        return StockMovement.Append(
            tenantId, branchId, productId, MovementType.Entrada,
            quantity, unitCost, lotId, originDocumentRef, performedByUserId,
            counterpartyBranchId: null);
    }

    public static StockMovement ApplyOut(
        StockBalance balance,
        TenantId tenantId,
        Guid branchId,
        ProductId productId,
        Quantity quantity,
        LotId? lotId,
        string? originDocumentRef,
        Guid performedByUserId)
    {
        balance.ApplyOut(quantity);

        return StockMovement.Append(
            tenantId, branchId, productId, MovementType.Saida,
            quantity, unitCost: null, lotId, originDocumentRef, performedByUserId,
            counterpartyBranchId: null);
    }

    public static StockMovement ApplyAdjustment(
        StockBalance balance,
        TenantId tenantId,
        Guid branchId,
        ProductId productId,
        decimal deltaQuantity,
        string reason,
        Guid performedByUserId)
    {
        balance.ApplyAdjustment(deltaQuantity);

        var movement = StockMovement.Append(
            tenantId, branchId, productId, MovementType.Ajuste,
            Quantity.Positive(Math.Abs(deltaQuantity)),
            unitCost: null, lotId: null, originDocumentRef: reason,
            performedByUserId, counterpartyBranchId: null);

        return movement;
    }

    /// <summary>
    /// Transferência entre filiais do MESMO tenant: par espelhado
    /// (TransferOut na origem + TransferIn no destino), mesma transação.
    /// </summary>
    public static (StockMovement Out, StockMovement In) Transfer(
        StockBalance fromBalance,
        StockBalance toBalance,
        TenantId tenantId,
        Guid fromBranchId,
        Guid toBranchId,
        ProductId productId,
        Quantity quantity,
        Guid performedByUserId)
    {
        if (fromBranchId == toBranchId)
            throw new BusinessRuleViolationException(
                "A filial de origem e de destino devem ser diferentes.");

        fromBalance.ApplyOut(quantity);
        toBalance.ApplyIn(quantity, unitCost: null);

        var @out = StockMovement.Append(
            tenantId, fromBranchId, productId, MovementType.TransferenciaSaida,
            quantity, unitCost: null, lotId: null,
            originDocumentRef: $"transfer:{toBranchId}", performedByUserId,
            counterpartyBranchId: toBranchId);

        var @in = StockMovement.Append(
            tenantId, toBranchId, productId, MovementType.TransferenciaEntrada,
            quantity, unitCost: null, lotId: null,
            originDocumentRef: $"transfer:{fromBranchId}", performedByUserId,
            counterpartyBranchId: fromBranchId);

        return (@out, @in);
    }
}
