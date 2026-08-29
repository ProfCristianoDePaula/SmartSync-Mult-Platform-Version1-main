using Estoque.Application.Repositories;

namespace Estoque.Application.Common;

/// <summary>
/// Unidade de trabalho: commit transacional dos agregados carregados pelos
/// repositórios (implementada sobre o EstoqueDbContext).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
