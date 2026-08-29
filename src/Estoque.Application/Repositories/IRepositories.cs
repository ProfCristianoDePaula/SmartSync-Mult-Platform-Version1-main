using Estoque.Domain.Common;
using Estoque.Domain.Entities;
using Estoque.Domain.Enums;

namespace Estoque.Application.Repositories;

// ---------------------------------------------------------------------------
// Catálogo
// ---------------------------------------------------------------------------

public interface IProductRepository
{
    Task<Product?> GetAsync(TenantId tenantId, Guid productId, CancellationToken ct = default);
    Task<Product?> GetBySkuAsync(TenantId tenantId, string sku, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(TenantId tenantId, string barcode, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int Total)> ListAsync(
        TenantId tenantId, string? search, Guid? categoryId, Guid? brandId,
        bool includeInactive, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Catálogo inteiro do tenant (importação XML / jobs) — sem paginação.</summary>
    Task<IReadOnlyList<Product>> ListBySkusAsync(TenantId tenantId, IEnumerable<string> skus, CancellationToken ct = default);
}

public interface IBrandRepository
{
    Task<Brand?> GetAsync(TenantId tenantId, Guid brandId, CancellationToken ct = default);
    Task<Brand?> GetByNameAsync(TenantId tenantId, string name, CancellationToken ct = default);
    Task AddAsync(Brand brand, CancellationToken ct = default);
    Task<(IReadOnlyList<Brand> Items, int Total)> ListAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default);
}

public interface IModelRepository
{
    Task<Model?> GetAsync(TenantId tenantId, Guid modelId, CancellationToken ct = default);
    Task<Model?> GetByNameAsync(TenantId tenantId, Guid brandId, string name, CancellationToken ct = default);
    Task AddAsync(Model model, CancellationToken ct = default);
    Task<(IReadOnlyList<Model> Items, int Total)> ListAsync(TenantId tenantId, Guid? brandId, int page, int pageSize, CancellationToken ct = default);
}

public interface ICategoryRepository
{
    Task<Category?> GetAsync(TenantId tenantId, Guid categoryId, CancellationToken ct = default);
    Task<Category?> GetByNameAsync(TenantId tenantId, string name, CancellationToken ct = default);
    Task AddAsync(Category category, CancellationToken ct = default);
    Task<(IReadOnlyList<Category> Items, int Total)> ListAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default);
}

public interface ISupplierRepository
{
    Task<Supplier?> GetAsync(TenantId tenantId, Guid supplierId, CancellationToken ct = default);
    Task<Supplier?> GetByDocumentAsync(TenantId tenantId, string documentNumber, CancellationToken ct = default);
    Task AddAsync(Supplier supplier, CancellationToken ct = default);
    Task<(IReadOnlyList<Supplier> Items, int Total)> ListAsync(
        TenantId tenantId, string? search, int page, int pageSize, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Movimentações / Saldos
// ---------------------------------------------------------------------------

public interface IStockBalanceRepository
{
    Task<StockBalance?> GetAsync(TenantId tenantId, Guid branchId, Guid productId, CancellationToken ct = default);
    Task AddAsync(StockBalance balance, CancellationToken ct = default);
    Task<(IReadOnlyList<StockBalance> Items, int Total)> ListAsync(
        TenantId tenantId, Guid? branchId, int page, int pageSize, CancellationToken ct = default);
}

public interface IStockMovementRepository
{
    Task AddRangeAsync(IReadOnlyList<StockMovement> movements, CancellationToken ct = default);
    Task<(IReadOnlyList<StockMovement> Items, int Total)> ListAsync(
        TenantId tenantId, Guid? branchId, Guid? productId, MovementType? type,
        int page, int pageSize, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Políticas
// ---------------------------------------------------------------------------

public interface IStockRuleRepository
{
    Task<StockRule?> GetEffectiveAsync(TenantId tenantId, ProductId productId, BranchId? branchId, CancellationToken ct = default);
    Task<StockRule?> FindExactAsync(TenantId tenantId, Guid productId, Guid? branchId, CancellationToken ct = default);
    Task AddAsync(StockRule rule, CancellationToken ct = default);
    Task<(IReadOnlyList<StockRule> Items, int Total)> ListAsync(TenantId tenantId, int page, int pageSize, CancellationToken ct = default);
}

public interface ILotRepository
{
    Task<Lot?> GetAsync(TenantId tenantId, Guid lotId, CancellationToken ct = default);
    Task<Lot?> GetByNumberAsync(TenantId tenantId, Guid branchId, Guid productId, string number, CancellationToken ct = default);
    Task AddAsync(Lot lot, CancellationToken ct = default);
    Task<IReadOnlyList<Lot>> ListExpiringAsync(TenantId tenantId, DateOnly today, int windowDays, Guid? branchId, CancellationToken ct = default);
}

public interface IOutletItemRepository
{
    Task<OutletItem?> GetOpenAsync(TenantId tenantId, Guid outletItemId, CancellationToken ct = default);
    Task AddAsync(OutletItem outlet, CancellationToken ct = default);
    Task<(IReadOnlyList<OutletItem> Items, int Total)> ListOpenAsync(
        TenantId tenantId, Guid? branchId, int page, int pageSize, CancellationToken ct = default);
}

public interface IAlertRepository
{
    Task AddAsync(Alert alert, CancellationToken ct = default);
    /// <summary>Dedupe: já existe alerta não reconhecido do mesmo tipo/referência no dia?</summary>
    Task<bool> ExistsOnDateAsync(TenantId tenantId, AlertType type, Guid? branchId, Guid? productId, DateOnly day, CancellationToken ct = default);
    Task<Alert?> GetAsync(TenantId tenantId, Guid alertId, CancellationToken ct = default);
    Task<(IReadOnlyList<Alert> Items, int Total)> ListAsync(
        TenantId tenantId, bool unacknowledgedOnly, AlertType? type, int page, int pageSize, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// AutoCompra / Integração
// ---------------------------------------------------------------------------

public interface IPurchaseSuggestionRepository
{
    Task<PurchaseSuggestion?> GetAsync(TenantId tenantId, Guid suggestionId, CancellationToken ct = default);
    Task<bool> HasOpenAsync(TenantId tenantId, ProductId productId, BranchId branchId, CancellationToken ct = default);
    Task AddAsync(PurchaseSuggestion suggestion, CancellationToken ct = default);
    Task<(IReadOnlyList<PurchaseSuggestion> Items, int Total)> ListAsync(
        TenantId tenantId, bool openOnly, int page, int pageSize, CancellationToken ct = default);
}

public interface IXmlImportRepository
{
    Task AddAsync(XmlImport import, CancellationToken ct = default);
    Task<XmlImport?> GetAsync(TenantId tenantId, Guid importId, CancellationToken ct = default);
    Task<XmlImport?> GetNextPendingAsync(CancellationToken ct = default);
}
