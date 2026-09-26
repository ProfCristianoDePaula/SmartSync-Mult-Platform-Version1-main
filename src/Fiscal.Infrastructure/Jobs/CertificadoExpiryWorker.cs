using Fiscal.Infrastructure.Certificados;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fiscal.Infrastructure.Jobs;

/// <summary>Varredura diária de validade de certificados (R4: renovação planejada).</summary>
public sealed class CertificadoExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CertificadoExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var svc = scope.ServiceProvider.GetRequiredService<ValidadeCertificadoService>();
                await svc.VerificarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CertificadoExpiryWorker falhou; nova tentativa em 24h.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
