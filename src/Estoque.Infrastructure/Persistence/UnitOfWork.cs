using Estoque.Application.Common;
using Estoque.Domain.Common;
using Estoque.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence;

public sealed class UnitOfWork(EstoqueDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => dbContext.SaveChangesAsync(ct);
}
