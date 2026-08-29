using Estoque.Application.Repositories;
using Estoque.Domain.ValueObjects;
using Estoque.Infrastructure.Persistence;
using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Infrastructure.Services.Integracao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Infrastructure.Jobs;

/// <summary>
/// Processa a fila de importações XML (NF-e) — extrai itens &lt;det&gt;/&lt;prod&gt;,
/// cria produtos ausentes (SKU = cProd, nome = xProd, UN = uCom normalizada)
/// e registra ENTRADAS de estoque na filial da importação. Idempotente via
/// máquina de estados do XmlImport.
/// </summary>
public sealed class XmlImportWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<XmlImportWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessNextAsync(stoppingToken); }
            catch (Exception ex)
            {
                logger.LogError(ex, "XmlImportWorker falhou ao processar importação.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessNextAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var importsRepo = scope.ServiceProvider.GetRequiredService<IXmlImportRepository>();

        var import = await importsRepo.GetNextPendingAsync(ct);
        if (import is null)
            return;

        import.MarkProcessing();
        var uow = scope.ServiceProvider.GetRequiredService<Application.Common.IUnitOfWork>();
        await uow.SaveChangesAsync(ct);

        try
        {
            var items = NfeXmlParser.Parse(import.Content);
            if (items.Count == 0)
            {
                import.Complete(totalItems: 0, processedItems: 0, skippedItems: 0);
                await uow.SaveChangesAsync(ct);
                return;
            }

            var productsRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var balancesRepo = scope.ServiceProvider.GetRequiredService<IStockBalanceRepository>();
            var movementsRepo = scope.ServiceProvider.GetRequiredService<IStockMovementRepository>();
            var lotsRepo = scope.ServiceProvider.GetRequiredService<ILotRepository>();
            var txFactory = scope.ServiceProvider.GetRequiredService<Application.Common.ITransactionScopeFactory>();

            int processed = 0, skipped = 0;

            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                // SKU inválido → item ignorado (importação parcial).
                Estoque.Domain.Entities.Product? product;
                try { product = await productsRepo.GetBySkuAsync(import.TenantId, item.CProd, ct); }
                catch (ArgumentException) { product = null; }

                if (product is null)
                {
                    try
                    {
                        product = Estoque.Domain.Entities.Product.Create(
                            import.TenantId, item.CProd,
                            string.IsNullOrWhiteSpace(item.XProd) ? item.CProd : item.XProd,
                            barcode: null, brandId: null, modelId: null, categoryId: null,
                            MapUom(item.UCom));
                        await productsRepo.AddAsync(product, ct);
                    }
                    catch (Exception ex) when (ex is ArgumentException or Domain.Common.BusinessRuleViolationException)
                    {
                        skipped++;
                        continue;
                    }
                }

                if (item.QCom <= 0)
                {
                    skipped++;
                    continue;
                }

                await using var tx = await txFactory.BeginTransactionAsync(ct);

                var balance = await balancesRepo.GetAsync(import.TenantId, import.BranchId.Value, product.Id.Value, ct)
                              ?? new StockBalance(import.TenantId, import.BranchId.Value, product.Id);

                if (balance.Id == default)
                    await balancesRepo.AddAsync(balance, ct);

                LotId? lotId = null;
                if (!string.IsNullOrWhiteSpace(item.Lote) && item.Validade is not null)
                {
                    var lot = await lotsRepo.GetByNumberAsync(
                                  import.TenantId, import.BranchId.Value, product.Id.Value, item.Lote, ct);
                    if (lot is not null)
                        lot.Add(Quantity.Positive(item.QCom));
                    else
                    {
                        lot = Lot.Create(import.TenantId, import.BranchId.Value, product.Id.Value,
                                         import.SupplierId?.Value, item.Lote, item.Validade.Value, item.QCom);
                        await lotsRepo.AddAsync(lot, ct);
                    }
                    lotId = lot.Id;
                }

                balance.ApplyIn(Quantity.Positive(item.QCom),
                                item.VUnCom > 0 ? Money.Create(item.VUnCom) : null);

                var movement = Domain.Services.MovementApplier.ApplyIn(
                    balance, import.TenantId, import.BranchId.Value, product.Id,
                    Quantity.Positive(item.QCom),
                    item.VUnCom > 0 ? Money.Create(item.VUnCom) : null,
                    lotId, originDocumentRef: $"nfe:{import.FileName}", 
                    performedByUserId: import.RequestedByUserId);

                await movementsRepo.AddRangeAsync([movement], ct);
                await uow.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                processed++;
            }

            import.Complete(items.Count, processed, skipped);
            await uow.SaveChangesAsync(ct);
            logger.LogInformation("Importação {File}: {P} processado(s), {S} ignorado(s).",
                import.FileName, processed, skipped);
        }
        catch (Exception ex)
        {
            import.Fail($"Falha no processamento: {ex.Message}");
            await uow.SaveChangesAsync(ct);
            throw;
        }
    }

    private static Domain.Enums.UnitOfMeasure MapUom(string? uCom) => (uCom ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "KG" or "KGM" => Domain.Enums.UnitOfMeasure.Quilograma,
        "G" or "GR" or "GRM" => Domain.Enums.UnitOfMeasure.Grama,
        "L" or "LTR" => Domain.Enums.UnitOfMeasure.Litro,
        "ML" or "MTQ" => Domain.Enums.UnitOfMeasure.Mililitro,
        "CX" => Domain.Enums.UnitOfMeasure.Caixa,
        "M" or "MTR" => Domain.Enums.UnitOfMeasure.Metro,
        _ => Domain.Enums.UnitOfMeasure.Unidade
    };
}



