using Estoque.Application.AutoCompra;
using Estoque.Application.Catalogo;
using Estoque.Application.Common;
using Estoque.Application.Integracao;
using Estoque.Application.IntegrationServices;
using Estoque.Application.Movimentacoes;
using Estoque.Application.PedidosVendas;
using Estoque.Application.Politicas;
using Estoque.Application.Repositories;
using Estoque.Infrastructure.Identity;
using Estoque.Infrastructure.Jobs;
using Estoque.Infrastructure.Persistence;
using Estoque.Infrastructure.Services.AutoCompra;
using Estoque.Infrastructure.Services.Catalogo;
using Estoque.Infrastructure.Services.Integracao;
using Estoque.Infrastructure.Services.Movimentacoes;
using Estoque.Infrastructure.Services.Politicas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Estoque.Infrastructure.Persistence.Repositories;
using Estoque.Infrastructure.Persistence.Outbox;

namespace Estoque.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEstoqueInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        services.AddDbContext<EstoqueDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Cliente do Identity (JWKS + gates) — seção "Identity".
        services.Configure<IdentityClientOptions>(
            configuration.GetSection(IdentityClientOptions.SectionName));
        services.AddHttpClient("identity", (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityClientOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                client.BaseAddress = new Uri(opts.BaseUrl);
        });
        services.AddHttpContextAccessor();
        services.AddSingleton<IdentityApiClient>();
        services.AddSingleton<JwksKeyStore>();
        services.AddHostedService<JwksRefreshService>();
        services.AddSingleton<IModuleAccessChecker, ModuleAccessChecker>();
        services.AddSingleton<IFilialAccessChecker, FilialAccessChecker>();

        // Unidade de trabalho + transações + outbox.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITransactionScopeFactory, TransactionScopeFactory>();
        services.AddScoped<OutboxService>();

        // Repositórios.
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBrandRepository, BrandRepository>();
        services.AddScoped<IModelRepository, ModelRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IStockBalanceRepository, StockBalanceRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IStockRuleRepository, StockRuleRepository>();
        services.AddScoped<ILotRepository, LotRepository>();
        services.AddScoped<IOutletItemRepository, OutletItemRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IPurchaseSuggestionRepository, PurchaseSuggestionRepository>();
        services.AddScoped<IXmlImportRepository, XmlImportRepository>();
        services.AddScoped<ICupomRepository, CupomRepository>();
        services.AddScoped<ICupomProdutoRepository, CupomProdutoRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IProdutosPedidoRepository, ProdutosPedidoRepository>();
        services.AddScoped<IVendaRepository, VendaRepository>();
        services.AddScoped<IProdutosVendaRepository, ProdutosVendaRepository>();
        services.AddScoped<IFormaPagtoRepository, FormaPagtoRepository>();

        // Serviços de aplicação (implementações).
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IModelService, ModelService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IStockBalanceService, StockBalanceService>();
        services.AddScoped<IStockRuleService, StockRuleService>();
        services.AddScoped<ILotService, LotService>();
        services.AddScoped<IOutletItemService, OutletItemService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IPurchaseSuggestionService, PurchaseSuggestionService>();
        services.AddScoped<IXmlImportService, XmlImportService>();
        // Pedidos e Vendas (Etapas 3-5)
        services.AddScoped<ICupomService, Services.PedidosVendas.CupomService>();
        services.AddScoped<IAplicarCupomService, Services.PedidosVendas.AplicarCupomService>();
        services.AddScoped<IPedidoService, Services.PedidosVendas.PedidoService>();
        services.AddScoped<ICalculoFreteService, Services.PedidosVendas.CalculoFreteService>();
        services.AddScoped<ITenantParcelamentoProvider, Services.PedidosVendas.TenantParcelamentoProvider>();
        services.AddScoped<IFormaPagtoService, Services.PedidosVendas.FormaPagtoService>();
        services.AddScoped<IVendaService, Services.PedidosVendas.VendaService>();
        services.AddScoped<ICheckoutService, Services.PedidosVendas.CheckoutService>();

        // Background jobs (BackgroundService nativo — sem dependências novas).
        services.AddHostedService<ExpiryScanWorker>();
        services.AddHostedService<ReplenishmentScanWorker>();
        services.AddHostedService<XmlImportWorker>();
        services.AddHostedService<OutboxDispatcherWorker>();

        return services;
    }
}

