namespace Estoque.Application.Common;

/// <summary>Transação de banco — abstração sem dependência de EF na Application.</summary>
public interface IDatabaseTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}

/// <summary>
/// Fábrica de transações usada pelos serviços que precisam de atomicidade
/// entre múltiplos agregados (movimentação + saldo + outbox).
/// </summary>
public interface ITransactionScopeFactory
{
    Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
