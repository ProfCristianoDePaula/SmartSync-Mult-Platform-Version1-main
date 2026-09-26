using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fiscal.Infrastructure.Persistence;

/// <summary>
/// Aplica migrations automaticamente na inicialização (padrão IdentitySeeder
/// do Identity) + seeds idempotentes do catálogo fiscal (Fiscal-2: 27 UFs e
/// endpoints SEFAZ verificados — R3: só com fonte oficial).
/// </summary>
public static class FiscalDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FiscalDbContext>();
        await dbContext.Database.MigrateAsync();
        await FiscalCatalogSeed.SeedAsync(dbContext);
        await FiscalNfseSeed.SeedAsync(dbContext);
    }
}
