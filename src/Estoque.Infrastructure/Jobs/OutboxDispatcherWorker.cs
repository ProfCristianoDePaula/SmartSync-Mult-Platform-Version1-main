using Estoque.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Infrastructure.Jobs;

/// <summary>
/// Dispatcher da outbox: marca eventos de integração como publicados (polling
/// dos consumidores). Quando um broker for introduzido (RabbitMQ), este worker
/// passa a PUBLICAR nele — a tabela outbox permanece a fonte da verdade.
/// </summary>
public sealed class OutboxDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxDispatcherWorker falhou; nova tentativa no próximo ciclo.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.EstoqueDbContext>();

        // Publicação v1: os consumidores leem via polling/replicação; aqui apenas
        // marcamos como disponíveis após o período de retenção mínima.
        var pending = await db.OutboxMessages
            .Where(m => m.PublishedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        foreach (var message in pending)
            message.PublishedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogDebug("Outbox: {Count} evento(s) disponibilizado(s).", pending.Count);
    }
}


