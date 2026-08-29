using Estoque.Application.Common;

namespace Estoque.Application.Movimentacoes;

public interface IStockMovementService
{
    Task<StockMovementDto> RegisterInAsync(RegisterStockInCommand command, CancellationToken ct = default);
    Task<StockMovementDto> RegisterOutAsync(RegisterStockOutCommand command, CancellationToken ct = default);
    Task<StockMovementDto> AdjustAsync(AdjustStockCommand command, CancellationToken ct = default);
    /// <summary>Transferência atômica: par espelhado de movimentações (saída + entrada).</summary>
    Task<(StockMovementDto Out, StockMovementDto In)> TransferAsync(TransferStockCommand command, CancellationToken ct = default);
    Task<PagedResult<StockMovementDto>> ListAsync(ListMovementsQuery query, CancellationToken ct = default);
}

public interface IStockBalanceService
{
    Task<PagedResult<StockBalanceDto>> ListAsync(ListBalancesQuery query, CancellationToken ct = default);
}
