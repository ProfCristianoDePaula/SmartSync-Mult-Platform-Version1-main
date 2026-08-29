using Estoque.Application.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace Estoque.Infrastructure.Persistence;

public sealed class EfDatabaseTransaction(IDbContextTransaction transaction) : IDatabaseTransaction
{
    public async Task CommitAsync(CancellationToken ct = default)
        => await transaction.CommitAsync(ct);

    public async ValueTask DisposeAsync()
        => await transaction.DisposeAsync();
}

public sealed class TransactionScopeFactory(EstoqueDbContext dbContext) : ITransactionScopeFactory
{
    public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => new EfDatabaseTransaction(await dbContext.Database.BeginTransactionAsync(ct));
}
