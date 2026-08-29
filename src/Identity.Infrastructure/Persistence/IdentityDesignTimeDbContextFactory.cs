using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` when the startup project does not
/// register the DbContext itself. Reads the connection string from the
/// standard configuration sources (env vars first), falling back to a local
/// dev constant — never a committed secret.
/// </summary>
public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string LocalDevConnection = "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? config["ConnectionStrings__DefaultConnection"]
                               ?? LocalDevConnection;

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new IdentityDbContext(options);
    }
}