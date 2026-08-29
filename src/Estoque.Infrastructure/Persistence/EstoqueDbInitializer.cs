using Estoque.Domain.Entities;
using Estoque.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Infrastructure.Persistence;

/// <summary>
/// Aplica migrations automaticamente na inicialização (padrão IdentitySeeder
/// do Identity). Não há seed de negócio: dados são criados pelos tenants.
/// </summary>
public static class EstoqueDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
