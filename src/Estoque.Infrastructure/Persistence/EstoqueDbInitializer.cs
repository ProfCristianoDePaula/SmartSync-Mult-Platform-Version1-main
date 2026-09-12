using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Estoque.Infrastructure.Persistence;

/// <summary>
/// Aplica migrations automaticamente na inicialização (padrão IdentitySeeder
/// do Identity) + seeds idempotentes do módulo Pedidos e Vendas (Etapa 6).
/// </summary>
public static class EstoqueDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();
        await dbContext.Database.MigrateAsync();
        await SeedAsync(dbContext);
    }

    private static async Task SeedAsync(EstoqueDbContext db)
    {
        // FormaPagto: 5 formas padrão (Etapa 6) — HasData alternativo seria migration, aqui é seeder dedicado idempotente
        if (!await db.FormasPagto.AnyAsync())
        {
            var formas = new[]
            {
                FormaPagto.FromValidated(FormaPagtoId.From(Guid.Parse("11111111-1111-1111-1111-111111111111")), "Pix", 1),
                FormaPagto.FromValidated(FormaPagtoId.From(Guid.Parse("22222222-2222-2222-2222-222222222222")), "Transferência", 1),
                FormaPagto.FromValidated(FormaPagtoId.From(Guid.Parse("33333333-3333-3333-3333-333333333333")), "Depósito", 1),
                FormaPagto.FromValidated(FormaPagtoId.From(Guid.Parse("44444444-4444-4444-4444-444444444444")), "Cartão de Débito", 1),
                FormaPagto.FromValidated(FormaPagtoId.From(Guid.Parse("55555555-5555-5555-5555-555555555555")), "Cartão de Crédito", 12),
            };
            await db.FormasPagto.AddRangeAsync(formas);
            await db.SaveChangesAsync();
        }
    }
}
