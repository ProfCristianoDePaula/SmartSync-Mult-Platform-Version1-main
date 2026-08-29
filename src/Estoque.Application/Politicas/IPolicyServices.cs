using Estoque.Application.Common;
using Estoque.Domain.Common;

namespace Estoque.Application.Politicas;

public interface IStockRuleService
{
    /// <summary>Cria ou substitui (upsert) a regra de (tenant, produto[, filial]).</summary>
    Task<StockRuleDto> SetAsync(SetStockRuleCommand command, CancellationToken ct = default);
    Task<PagedResult<StockRuleDto>> ListAsync(TenantId tenantId, int page = 1, int pageSize = 50, CancellationToken ct = default);
}

public interface ILotService
{
    Task<PagedResult<LotDto>> ListExpiringAsync(ListExpiringLotsQuery query, CancellationToken ct = default);
}

public interface IOutletItemService
{
    Task<OutletDto> MarkAsync(MarkOutletCommand command, CancellationToken ct = default);
    Task<bool> ResolveAsync(ResolveOutletCommand command, CancellationToken ct = default);
    Task<PagedResult<OutletDto>> ListOpenAsync(TenantId tenantId, Guid? branchId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public interface IAlertService
{
    Task<PagedResult<AlertDto>> ListAsync(ListAlertsQuery query, CancellationToken ct = default);
    Task<bool> AcknowledgeAsync(AcknowledgeAlertCommand command, CancellationToken ct = default);
}
