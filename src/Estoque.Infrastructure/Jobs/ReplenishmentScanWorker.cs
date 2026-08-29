using Estoque.Application.Repositories;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Estoque.Domain.Services;
using Estoque.Domain.ValueObjects;
using Estoque.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Infrastructure.Jobs;

/// <summary>
/// Motor de reposição/AutoCompra: varre saldos vs regras (override de filial
/// tem prioridade) e gera alertas de ruptura/excesso + sugestões de compra
/// (uma aberta por produto×filial). Executa a cada hora; idempotente.
/// </summary>
public sealed class ReplenishmentScanWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReplenishmentScanWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequeno atraso inicial para não competir com o startup.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ScanAsync(stoppingToken); }
            catch (Exception ex)
            {
                logger.LogError(ex, "ReplenishmentScanWorker falhou; continuará no próximo ciclo.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();

        var balances = await db.StockBalances.AsNoTracking().ToListAsync(ct);
        if (balances.Count == 0)
            return;

        var rulesRepo = scope.ServiceProvider.GetRequiredService<IStockRuleRepository>();
        var alertsRepo = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        var suggestionsRepo = scope.ServiceProvider.GetRequiredService<IPurchaseSuggestionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<Application.Common.IUnitOfWork>();

        int ruptures = 0, excess = 0, suggestions = 0;

        foreach (var balance in balances)
        {
            ct.ThrowIfCancellationRequested();

            var rule = await rulesRepo.GetEffectiveAsync(balance.TenantId, balance.ProductId, balance.BranchId, ct);
            if (rule is null)
                continue;

            var (rupture, overstock, toBuy) = ReplenishmentPolicy.Evaluate(balance, rule);

            if (rupture is not null && !await alertsRepo.ExistsOnDateAsync(
                    balance.TenantId, AlertType.Ruptura, balance.BranchId.Value,
                    balance.ProductId.Value, Today, ct))
            {
                await alertsRepo.AddAsync(Alert.Create(
                    balance.TenantId, balance.BranchId.Value, balance.ProductId.Value, null,
                    AlertType.Ruptura, AlertSeverity.Critico,
                    $"Ruptura: saldo {balance.Quantity} abaixo do mínimo {rule.MinimumQuantity}."),
                    ct);
                ruptures++;
            }

            if (overstock is not null && !await alertsRepo.ExistsOnDateAsync(
                    balance.TenantId, AlertType.Excesso, balance.BranchId.Value,
                    balance.ProductId.Value, Today, ct))
            {
                await alertsRepo.AddAsync(Alert.Create(
                    balance.TenantId, balance.BranchId.Value, balance.ProductId.Value, null,
                    AlertType.Excesso, AlertSeverity.Info,
                    $"Excesso: saldo {balance.Quantity} acima de 3x o ponto de pedido ({rule.ReorderPoint})."),
                    ct);
                excess++;
            }

            if (toBuy is not null &&
                !await suggestionsRepo.HasOpenAsync(balance.TenantId, balance.ProductId, balance.BranchId, ct))
            {
                var suggestion = PurchaseSuggestion.Create(
                    balance.TenantId, balance.ProductId.Value, balance.BranchId.Value,
                    supplierId: null, toBuy.SuggestedQuantity, toBuy.ObservedBalance);
                await suggestionsRepo.AddAsync(suggestion, ct);
                suggestions++;
            }
        }

        await uow.SaveChangesAsync(ct);
        logger.LogInformation(
            "Reposição concluída: {Ruptures} ruptura(s), {Excess} excesso(s), {Suggestions} sugestão(ões).",
            ruptures, excess, suggestions);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}


