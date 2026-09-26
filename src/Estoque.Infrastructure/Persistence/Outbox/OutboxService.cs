using System.Text.Json;
using System.Text.Json.Serialization;
using Estoque.Domain.Events;

namespace Estoque.Infrastructure.Persistence.Outbox;

/// <summary>
/// Escrita/leitura da outbox. Os eventos de domínio são serializados na MESMA
/// transação do negócio (Transactional Outbox, decisão da Etapa 23).
/// </summary>
public sealed class OutboxService(EstoqueDbContext dbContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(IEnumerable<object> domainEvents, CancellationToken ct = default)
    {
        foreach (var @event in domainEvents)
        {
            var type = ResolveTypeSlug(@event.GetType().Name);
            if (type is null)
                continue; // eventos não mapeados não vão para integração

            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Type = type,
                PayloadJson = JsonSerializer.Serialize(@event, @event.GetType(), JsonOptions)
            });
        }

        await Task.CompletedTask;
    }

    public Task<List<OutboxMessage>> DequeuePendingAsync(int batchSize, CancellationToken ct = default)
    {
        // Síncrono de propósito: evita ambiguidade entre EF ToListAsync e
        // System.Linq.Async (presente via OpenTelemetry).
        var list = dbContext.OutboxMessages
            .Where(m => m.PublishedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(batchSize)
            .ToList();

        return Task.FromResult(list);
    }

    /// <summary>Marca eventos como "publicados" para consumo pelos demais serviços.</summary>
    public async Task MarkPublishedAsync(IReadOnlyList<OutboxMessage> messages, CancellationToken ct = default)
    {
        foreach (var message in messages)
            message.PublishedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }

    private static string? ResolveTypeSlug(string eventName) => eventName switch
    {
        nameof(ProductCreated) => "estoque.produto.criado",
        nameof(ProductSoftDeleted) => "estoque.produto.inativado",
        nameof(StockMovementRegistered) => "estoque.movimentacao.registrada",
        nameof(LowStockDetected) => "estoque.alerta.ruptura",
        nameof(LotNearExpiry) => "estoque.alerta.validade",
        nameof(OutletMarked) => "estoque.outlet.marcado",
        nameof(PurchaseSuggestionCreated) => "estoque.autocompra.criada",
        nameof(VendaFinalizada) => "estoque.venda.finalizada",
        _ => null
    };
}


