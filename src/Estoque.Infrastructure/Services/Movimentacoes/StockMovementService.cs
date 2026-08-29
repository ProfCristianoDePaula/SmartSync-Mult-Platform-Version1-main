using Estoque.Application.Common;
using Estoque.Application.Movimentacoes;
using Estoque.Application.Repositories;
using Estoque.Domain.Common;
using Estoque.Domain.Services;
using Estoque.Domain.ValueObjects;
using Estoque.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore.Storage;
using Estoque.Domain.Entities;
using Estoque.Application.IntegrationServices;

namespace Estoque.Infrastructure.Services.Movimentacoes;

/// <summary>
/// Orquestra movimentações: valida filial/produto, aplica as invariantes via
/// MovementApplier e persiste saldo + movimentações + outbox numa ÚNICA
/// transação. Eventos são despachados pós-commit.
/// </summary>
public sealed class StockMovementService(
    IStockBalanceRepository balances,
    IStockMovementRepository movements,
    IProductRepository products,
    ILotRepository lots,
    IFilialAccessChecker filialChecker,
    IUnitOfWork uow,
    OutboxService outbox,
    ITransactionScopeFactory txFactory) : IStockMovementService
{
    public async Task<StockMovementDto> RegisterInAsync(RegisterStockInCommand command, CancellationToken ct = default)
    {
        var tenantId = command.TenantId;
        await EnsureBranchAccessible(tenantId, command.BranchId, ct);
        await EnsureProductActive(tenantId, command.ProductId, ct);

        await using var tx = await txFactory.BeginTransactionAsync(ct);

        var balance = await GetOrCreateBalanceAsync(tenantId, command.BranchId, command.ProductId, ct);

        // Lote: por ID existente OU cria/atualiza pelo número + validade.
        Lot? lot = null;
        if (command.LotId is not null)
        {
            lot = await lots.GetAsync(tenantId, command.LotId.Value, ct)
                  ?? throw new BusinessRuleViolationException("Lote não encontrado.");
            lot.Add(Quantity.Positive(command.Quantity));
        }
        else if (!string.IsNullOrWhiteSpace(command.LotNumber) && command.LotExpiresOn is not null)
        {
            lot = await lots.GetByNumberAsync(
                      tenantId, command.BranchId, command.ProductId, command.LotNumber, ct);

            if (lot is not null)
            {
                lot.Add(Quantity.Positive(command.Quantity));
            }
            else
            {
                lot = Lot.Create(tenantId, command.BranchId, command.ProductId,
                                 command.SupplierId, command.LotNumber!,
                                 command.LotExpiresOn.Value, command.Quantity);
                await lots.AddAsync(lot, ct);
            }
        }

        var movement = MovementApplier.ApplyIn(
            balance, tenantId, command.BranchId, ProductId.From(command.ProductId),
            Quantity.Positive(command.Quantity),
            command.UnitCost is null ? null : Money.Create(command.UnitCost.Value),
            lot?.Id, command.OriginDocumentRef, command.PerformedByUserId);

        await movements.AddRangeAsync([movement], ct);
        await outbox.WriteAsync(balance.PopEvents().Concat(movement.PopEvents()), ct);
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(movement);
    }

    public async Task<StockMovementDto> RegisterOutAsync(RegisterStockOutCommand command, CancellationToken ct = default)
    {
        var tenantId = command.TenantId;
        await EnsureBranchAccessible(tenantId, command.BranchId, ct);

        await using var tx = await txFactory.BeginTransactionAsync(ct);

        var balance = await balances.GetAsync(tenantId, command.BranchId, command.ProductId, ct)
                      ?? throw new BusinessRuleViolationException(
                          "Produto sem saldo nesta filial.");

        if (command.LotId is not null)
        {
            var lot = await lots.GetAsync(tenantId, command.LotId.Value, ct)
                      ?? throw new BusinessRuleViolationException("Lote não encontrado.");
            lot.Consume(Quantity.Positive(command.Quantity));
        }

        var movement = MovementApplier.ApplyOut(
            balance, tenantId, command.BranchId, ProductId.From(command.ProductId),
            Quantity.Positive(command.Quantity), command.LotId.HasValue ? LotId.From(command.LotId.Value) : null,
            command.OriginDocumentRef, command.PerformedByUserId);

        await movements.AddRangeAsync([movement], ct);
        await outbox.WriteAsync(balance.PopEvents().Concat(movement.PopEvents()), ct);
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(movement);
    }

    public async Task<StockMovementDto> AdjustAsync(AdjustStockCommand command, CancellationToken ct = default)
    {
        var tenantId = command.TenantId;
        await EnsureBranchAccessible(tenantId, command.BranchId, ct);

        await using var tx = await txFactory.BeginTransactionAsync(ct);

        var balance = await GetOrCreateBalanceAsync(tenantId, command.BranchId, command.ProductId, ct);

        var movement = MovementApplier.ApplyAdjustment(
            balance, tenantId, command.BranchId, ProductId.From(command.ProductId),
            command.DeltaQuantity, command.Reason, command.PerformedByUserId);

        await movements.AddRangeAsync([movement], ct);
        await outbox.WriteAsync(balance.PopEvents().Concat(movement.PopEvents()), ct);
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDto(movement);
    }

    public async Task<(StockMovementDto Out, StockMovementDto In)> TransferAsync(
        TransferStockCommand command, CancellationToken ct = default)
    {
        var tenantId = command.TenantId;
        await EnsureBranchAccessible(tenantId, command.FromBranchId, ct);
        await EnsureBranchAccessible(tenantId, command.ToBranchId, ct);

        await using var tx = await txFactory.BeginTransactionAsync(ct);

        var from = await balances.GetAsync(tenantId, command.FromBranchId, command.ProductId, ct)
                   ?? throw new BusinessRuleViolationException("Produto sem saldo na filial de origem.");
        var to = await GetOrCreateBalanceAsync(tenantId, command.ToBranchId, command.ProductId, ct);

        var (outMv, inMv) = MovementApplier.Transfer(
            from, to, tenantId, command.FromBranchId, command.ToBranchId,
            ProductId.From(command.ProductId), Quantity.Positive(command.Quantity),
            command.PerformedByUserId);

        await movements.AddRangeAsync([outMv, inMv], ct);
        await outbox.WriteAsync(from.PopEvents().Concat(to.PopEvents())
            .Concat(outMv.PopEvents()).Concat(inMv.PopEvents()), ct);
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (ToDto(outMv), ToDto(inMv));
    }

    public async Task<PagedResult<StockMovementDto>> ListAsync(ListMovementsQuery query, CancellationToken ct = default)
    {
        var (items, total) = await movements.ListAsync(
            query.TenantId, query.BranchId, query.ProductId, query.Type,
            query.Page, query.PageSize, ct);

        return new PagedResult<StockMovementDto>(
            items.Select(ToDto).ToList(), query.Page, query.PageSize, total,
            (int)Math.Ceiling(total / (double)query.PageSize));
    }

    private async Task<StockBalance> GetOrCreateBalanceAsync(
        TenantId tenantId, Guid branchId, Guid productId, CancellationToken ct)
    {
        var balance = await balances.GetAsync(tenantId, branchId, productId, ct);
        if (balance is not null)
            return balance;

        balance = new StockBalance(tenantId, branchId, ProductId.From(productId));
        await balances.AddAsync(balance, ct);
        return balance;
    }

    /// <summary>
    /// Posse da filial: Denied bloqueia; Unknown aplica a política de fallback
    /// documentada na Etapa 23 (aceitar escopado por tenant — risco residual
    /// intra-tenant baixo até o Identity expor /me/branches).
    /// </summary>
    private async Task EnsureBranchAccessible(TenantId tenantId, Guid branchId, CancellationToken ct)
    {
        var access = await filialChecker.ValidateAsync(tenantId, branchId, ct);
        if (access == Application.IntegrationServices.FilialAccess.Denied)
            throw new BusinessRuleViolationException("Filial não pertence ao seu tenant.", "branch-denied");
    }

    private async Task EnsureProductActive(TenantId tenantId, Guid productId, CancellationToken ct)
    {
        var product = await products.GetAsync(tenantId, productId, ct);
        if (product is null || !product.IsActive)
            throw new BusinessRuleViolationException("Produto não encontrado neste tenant.");
    }

    private static StockMovementDto ToDto(Domain.Entities.StockMovement m) => new(
        m.Id.Value, m.BranchId.Value, m.ProductId.Value, m.Type,
        m.Quantity.Value, m.UnitCost?.Amount, m.LotId?.Value,
        m.OriginDocumentRef, m.PerformedByUserId, m.CounterpartyBranchId, m.CreatedAtUtc);
}
