using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fiscal.Infrastructure.Persistence;

/// <summary>
/// Fábrica usada APENAS pelo dotnet-ef em tempo de design — evita que a
/// ferramenta execute o Program.cs da API (que aplica migrations).
/// Connection string de fallback; a real vem do ambiente em runtime.
/// </summary>
public sealed class FiscalDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FiscalDbContext>
{
    public FiscalDbContext CreateDbContext(string[] args)
        => new(
            new DbContextOptionsBuilder<FiscalDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=fiscal;Username=postgres;Password=postgres")
                .Options);
}
