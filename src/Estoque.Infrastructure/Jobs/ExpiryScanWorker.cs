using Estoque.Domain.Entities;
using Estoque.Domain.Enums;
using Estoque.Application.Repositories;
using Estoque.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Infrastructure.Jobs;

/// <summary>
/// Varredura DIÁRIA de lotes vencendo/vencidos → alertas de validade
/// (dedupe por dia/referência). Idempotente e tolerante a falhas.
/// </summary>
public sealed class ExpiryScanWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiryScanWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ExpiryScanWorker falhou; continuará no próximo ciclo.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = today.AddDays(30);

        // Lotes com quantidade, vencendo em 30 dias (ou vencidos), agrupados por tenant.
        var lots = await db.Lots
            .Where(l => l.IsActive && l.Quantity > Estoque.Domain.ValueObjects.Quantity.FromValidated(0m)
                        && l.ExpiresOn <= limit)
            .ToListAsync(ct);
        if (lots.Count == 0)
            return;

        var alertsRepo = scope.ServiceProvider.GetRequiredService<IAlertRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<Application.Common.IUnitOfWork>();

        foreach (var group in lots.GroupBy(l => l.TenantId))
        {
            foreach (var lot in group)
            {
                ct.ThrowIfCancellationRequested();

                if (await alertsRepo.ExistsOnDateAsync(
                        group.Key, AlertType.Validade, lot.BranchId.Value,
                        lot.ProductId.Value, today, ct))
                    continue;

                var expired = lot.ExpiresOn < today;
                var severity = expired ? AlertSeverity.Critico : AlertSeverity.Aviso;
                var message = expired
                    ? $"Lote {lot.Number} do produto {lot.ProductId} está VENCIDO desde {lot.ExpiresOn:dd/MM/yyyy}."
                    : $"Lote {lot.Number} do produto {lot.ProductId} vence em {lot.ExpiresOn:dd/MM/yyyy} ({(lot.ExpiresOn.DayNumber - today.DayNumber)} dias).";

                await alertsRepo.AddAsync(Alert.Create(
                    group.Key, lot.BranchId.Value, lot.ProductId.Value, lot.Id.Value,
                    AlertType.Validade, severity, message), ct);
            }
        }

        await uow.SaveChangesAsync(ct);
        logger.LogInformation("ExpiryScan concluído: {Count} lote(s) avaliado(s).", lots.Count);
    }
}


