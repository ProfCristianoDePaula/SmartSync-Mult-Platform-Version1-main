using Estoque.Application.Common;
using Estoque.Application.Movimentacoes;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;

namespace Estoque.Infrastructure.Services.Movimentacoes;

public sealed class StockBalanceService(
    IStockBalanceRepository balances,
    IStockRuleRepository rules,
    IProductRepository products) : IStockBalanceService
{
    /// <summary>
    /// Saldos por tenant/filial com enriquecimento de produto e filtro opcional
    /// de ruptura (abaixo do mínimo efetivo da regra).
    /// </summary>
    public async Task<PagedResult<StockBalanceDto>> ListAsync(ListBalancesQuery query, CancellationToken ct = default)
    {
        var (balancesList, total) = await balances.ListAsync(
            query.TenantId, query.BranchId, query.Page, query.PageSize, ct);

        // Enriquecimento com dados do catálogo (produto é do tenant — sem cópia).
        var productIds = balancesList.Select(b => b.ProductId.Value).Distinct().ToList();
        var catalog = new Dictionary<Guid, (string Sku, string Name)>();
        foreach (var productId in productIds)
        {
            var p = await products.GetAsync(query.TenantId, productId, ct);
            if (p is not null)
                catalog[productId] = (p.Sku.Value, p.Name);
        }

        Dictionary<(Guid Product, Guid Branch), decimal>? minimums = null;
        if (query.BelowMinimumOnly)
            minimums = await LoadEffectiveMinimumsAsync(query.TenantId, ct);

        var dtos = new List<StockBalanceDto>();
        foreach (var b in balancesList)
        {
            if (!catalog.TryGetValue(b.ProductId.Value, out var info))
                continue; // produto removido do catálogo

            if (query.BelowMinimumOnly)
            {
                var min = minimums!.GetValueOrDefault((b.ProductId.Value, b.BranchId.Value));
                if (min == 0 || b.Quantity.Value >= min)
                    continue; // sem regra ou acima do mínimo
            }

            dtos.Add(new StockBalanceDto(
                b.ProductId.Value, info.Sku, info.Name, b.BranchId.Value,
                b.Quantity.Value, b.AverageUnitCost?.Amount, b.UpdatedAtUtc));
        }

        return new PagedResult<StockBalanceDto>(dtos, query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private async Task<Dictionary<(Guid, Guid), decimal>> LoadEffectiveMinimumsAsync(TenantId tenantId, CancellationToken ct)
    {
        var map = new Dictionary<(Guid, Guid), decimal>();
        // Regras padrão do tenant (branch nula).
        var (defaultRules, _) = await rules.ListAsync(tenantId, 1, int.MaxValue, ct);
        foreach (var rule in defaultRules.Where(r => r.BranchId is null))
            map[(rule.ProductId.Value, Guid.Empty)] = rule.MinimumQuantity.Value;

        foreach (var rule in defaultRules.Where(r => r.BranchId is not null))
            map[(rule.ProductId.Value, rule.BranchId!.Value.Value)] = rule.MinimumQuantity.Value;

        return map;
    }
}
