using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Estoque.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada APENAS pelo dotnet-ef em tempo de design — evita que a
/// ferramenta execute o Program.cs da API (que aplica migrations/seeds).
/// Connection string de fallback; a real vem do ambiente em runtime.
/// </summary>
public sealed class EstoqueDesignTimeDbContextFactory : IDesignTimeDbContextFactory<EstoqueDbContext>
{
    public EstoqueDbContext CreateDbContext(string[] args)
        => new(
            new DbContextOptionsBuilder<EstoqueDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=estoque;Username=postgres;Password=postgres")
                .Options);
}
