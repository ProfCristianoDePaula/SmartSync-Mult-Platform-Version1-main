namespace Estoque.Infrastructure.Persistence.Outbox;

/// <summary>
/// Mensagem do Transactional Outbox — escrita NA MESMA TRANSAÇÃO da operação
/// de negócio; o OutboxDispatcherWorker marca como publicada. Consumidores
/// futuros (outros microsserviços) fazem polling até um broker ser introduzido.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    /// <summary>Tipo lógico do evento (ex.: "estoque.movimentacao.registrada").</summary>
    public string Type { get; init; } = null!;
    /// <summary>Payload JSON do evento.</summary>
    public string PayloadJson { get; init; } = null!;
    public DateTime? PublishedAtUtc { get; set; }
}
