using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Estoque.Application.Catalogo;
using Estoque.Application.Movimentacoes;
using Estoque.Domain.Enums;

using Xunit;
namespace Estoque.Tests;

/// <summary>
/// Smoke test de ponta a ponta do fluxo principal: catálogo → entrada →
/// saldo → saída → invariante de saldo insuficiente (400).
/// </summary>
[Collection("integration")]
public sealed class StockFlowTests(EstoqueApiFixture fixture)
{
    private HttpClient Client(string role = "TenantAdmin")
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.IssueToken(EstoqueApiFixture.TenantA, role));
        return client;
    }

    [Fact]
    public async Task Health_DeveRetornar200()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_Protegido_SemToken_DeveRetornar401()
    {
        using var client = fixture.Factory.CreateClient();
        var response = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FluxoCompleto_EntradaSaida_DeveManterInvariantes()
    {
        const string sku = "SMK-001";

        // 1. Marca + produto
        using var admin = Client();

        var brandResponse = await admin.PostAsJsonAsync("/api/brands", new { name = "Marca Smoke" });
        Assert.Equal(HttpStatusCode.Created, brandResponse.StatusCode);
        var brand = await brandResponse.Content.ReadFromJsonAsync<BrandDto>();

        var productResponse = await admin.PostAsJsonAsync("/api/products", new
        {
            sku,
            name = "Produto Smoke",
            barcode = (string?)null,
            brandId = brand!.Id,
            modelId = (Guid?)null,
            categoryId = (Guid?)null,
            unitOfMeasure = UnitOfMeasure.Unidade
        });
        if (productResponse.StatusCode != HttpStatusCode.Created)
        {
            var body = await productResponse.Content.ReadAsStringAsync();
            Assert.Fail($"POST /api/products => {productResponse.StatusCode}: {body}");
        }
        var product = await productResponse.Content.ReadFromJsonAsync<ProductDto>();

        // 2. SKU duplicado → 400
        var duplicate = await admin.PostAsJsonAsync("/api/products", new
        {
            sku,
            name = "Duplicado",
            barcode = (string?)null,
            unitOfMeasure = UnitOfMeasure.Unidade
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        // 3. Entrada de 10 unidades com custo
        var tenant = EstoqueApiFixture.TenantA;
        var branch = EstoqueApiFixture.BranchA;

        var stockIn = await admin.PostAsJsonAsync("/api/stock/in", new
        {
            branchId = branch,
            productId = product!.Id,
            quantity = 10m,
            unitCost = 5.5m,
            originDocumentRef = "smoke"
        });
        Assert.Equal(HttpStatusCode.Created, stockIn.StatusCode);

        // 4. Saída maior que o saldo → 400 (invariante saldo ≥ 0)
        var overOut = await admin.PostAsJsonAsync("/api/stock/out", new
        {
            branchId = branch,
            productId = product.Id,
            quantity = 11m
        });
        Assert.Equal(HttpStatusCode.BadRequest, overOut.StatusCode);

        // 5. Saída válida de 4 → saldo 6
        var stockOut = await admin.PostAsJsonAsync("/api/stock/out", new
        {
            branchId = branch,
            productId = product.Id,
            quantity = 4m
        });
        Assert.Equal(HttpStatusCode.Created, stockOut.StatusCode);

        // 6. Saldo refletido na consulta
        var balances = await admin.GetFromJsonAsync<PagedResultDto>($"/api/balances?branchId={branch}");
        var line = balances!.Items.Single(i => i.productId == product.Id);
        Assert.Equal(6m, line.quantity);
        Assert.Equal(product.Sku, line.sku);

        // 7. Transferência para outra filial do mesmo tenant
        var branchB = Guid.NewGuid();
        var transfer = await admin.PostAsJsonAsync("/api/stock/transfers", new
        {
            fromBranchId = branch,
            toBranchId = branchB,
            productId = product.Id,
            quantity = 2m
        });
        Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);

        // 8. Seller NÃO pode ajustar inventário → 403
        using var seller = Client("Seller");
        var adjust = await seller.PostAsJsonAsync("/api/stock/adjustments", new
        {
            branchId = branch,
            productId = product.Id,
            deltaQuantity = -1m,
            reason = "teste"
        });
        Assert.Equal(HttpStatusCode.Forbidden, adjust.StatusCode);
    }

    /// <summary>Espelho local do PagedResult para leitura JSON case-insensitive.</summary>
    public sealed record PagedResultDto(IReadOnlyList<BalanceItem> Items);
    public sealed record BalanceItem(Guid productId, string sku, string productName, Guid branchId, decimal quantity);
}


