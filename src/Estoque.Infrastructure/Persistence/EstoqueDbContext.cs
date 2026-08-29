using Estoque.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Estoque.Infrastructure.Persistence;

public sealed class EstoqueDbContext : DbContext
{
    public EstoqueDbContext(DbContextOptions<EstoqueDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Model> Models => Set<Model>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockRule> StockRules => Set<StockRule>();
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<OutletItem> OutletItems => Set<OutletItem>();
    public DbSet<PurchaseSuggestion> PurchaseSuggestions => Set<PurchaseSuggestion>();
    public DbSet<XmlImport> XmlImports => Set<XmlImport>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Outbox.OutboxMessage> OutboxMessages => Set<Outbox.OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EstoqueDbContext).Assembly);
    }
}
