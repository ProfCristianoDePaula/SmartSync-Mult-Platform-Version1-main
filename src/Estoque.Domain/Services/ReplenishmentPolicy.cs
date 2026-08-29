using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Domain.ValueObjects;

namespace Estoque.Domain.Services;

/// <summary>
/// Políticas puras de reposição e validade — sem I/O (testáveis em unidade).
/// Os jobs/infraestrutura alimentam com dados e persistem os resultados.
/// </summary>
public static class ReplenishmentPolicy
{
    public sealed record RuptureAlert(TenantId TenantId, Guid BranchId, ProductId ProductId,
        decimal Balance, decimal MinimumQty);

    public sealed record ExcessAlert(TenantId TenantId, Guid BranchId, ProductId ProductId,
        decimal Balance);

    public sealed record Suggestion(TenantId TenantId, Guid BranchId, ProductId ProductId,
        Guid? SupplierId, decimal SuggestedQuantity, decimal ObservedBalance);

    /// <summary>
    /// Avalia um saldo contra a regra efetiva (override de filial senão default do
    /// tenant). Retorna alerta de ruptura/excesso e sugestão quando aplicável.
    /// </summary>
    public static (RuptureAlert? Rupture, ExcessAlert? Excess, Suggestion? ToPurchase)
        Evaluate(StockBalance balance, StockRule? rule)
    {
        if (rule is null)
            return (null, null, null);

        var qty = balance.Quantity.Value;

        if (qty < rule.MinimumQuantity.Value)
        {
            var suggested = Math.Max(
                rule.ReorderPoint.Value * 2 - qty,
                rule.MinimumQuantity.Value - qty);

            return (
                new RuptureAlert(balance.TenantId, balance.BranchId.Value,
                    balance.ProductId, qty, rule.MinimumQuantity.Value),
                null,
                new Suggestion(balance.TenantId, balance.BranchId.Value, balance.ProductId,
                    SupplierId: null, suggested, qty));
        }

        // Acima do mínimo: sem alerta. Excesso é definido como 3x o ponto de pedido.
        if (rule.ReorderPoint.Value > 0 && qty > rule.ReorderPoint.Value * 3)
            return (
                null,
                new ExcessAlert(balance.TenantId, balance.BranchId.Value, balance.ProductId, qty),
                null);

        return (null, null, null);
    }

    public sealed record ExpiryCandidate(Lot Lot, int DaysUntilExpiry);

    /// <summary>Lotes vencendo dentro da janela informada (inclusive vencidos).</summary>
    public static IEnumerable<ExpiryCandidate> FindExpiring(
        IEnumerable<Lot> lots, DateOnly today, int windowDays)
    {
        foreach (var lot in lots)
        {
            if (lot.Quantity.Value <= 0 || !lot.IsActive)
                continue;

            var days = today.DayNumber - lot.ExpiresOn.DayNumber; // negativo = futuro
            if (-days <= windowDays) // vence hoje ou nos próximos N dias (ou já vencido)
                yield return new ExpiryCandidate(lot, days);
        }
    }

    public static AlertSeverity SeverityFor(int daysUntilExpiry)
        => daysUntilExpiry >= 0 ? AlertSeverity.Critico : AlertSeverity.Aviso;
}

