using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Branches;
using Identity.Application.Common;
using Identity.Application.TenantModules;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// CRUD de filiais (Etapa 14), sempre aninhado em /api/tenants/{tenantId}/branches.
/// Autorização por claim do JWT: SuperAdmin (global, sem claim tenant_id) opera em
/// qualquer tenant; TenantAdmin opera APENAS no próprio tenant (claim tenant_id
/// precisa bater com a rota, senão 403). Garante também que listagem/detalhe nunca
/// vazem filiais de outro tenant. Soft delete via query filter "Active".
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class BranchesTests
{
    private readonly IdentityApiFactory _factory;

    public BranchesTests(IdentityApiFactory factory) => _factory = factory;

    private static CreateBranchCommand NewBranch(string? name = null) => new(
        Guid.Empty,
        name ?? $"Filial {Guid.NewGuid():N}",
        new BranchAddress(
            "Rua das Flores",
            "123",
            "Sala 2",
            "Centro",
            "São Paulo",
            "SP",
            "01310-100"),
        new BranchContact("(11) 99999-1234", null, TestData.UniqueEmail("filial")));

    private static UpdateBranchCommand UpdateBranch(Guid tenantId, Guid branchId, string name)
        => new(
            tenantId,
            branchId,
            name,
            new BranchAddress(
                "Av. Paulista",
                "1000",
                null,
                "Bela Vista",
                "São Paulo",
                "SP",
                "01310-100"),
            new BranchContact("1130001122", "11988887766", TestData.UniqueEmail("filial")));

    private async Task<HttpClient> AuthClientAsync(string email)
    {
        var tokens = await TestData.LoginAsync(_factory, email, TestData.UniquePassword());
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    [Fact]
    public async Task CriarFilial_SuperAdmin_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var branch = await response.Content.ReadFromJsonAsync<BranchDto>();
        Assert.NotNull(branch);
        Assert.Equal(tenant.Id.Value, branch!.TenantId);
        Assert.True(branch.IsActive);
        Assert.Null(branch.DeletedAtUtc);
        Assert.Equal("01310100", branch.Address.PostalCode);
        Assert.Equal("11999991234", branch.Contact.Phone);
    }

    [Fact]
    public async Task CriarFilial_TenantInexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{Guid.NewGuid()}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_TenantSoftDeletado_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var tenant = await TestData.CreateTenantAsync(sp);
        var db = sp.GetRequiredService<IdentityDbContext>();
        tenant.SoftDelete();
        await db.SaveChangesAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{Guid.NewGuid()}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_Cliente_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var clientUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var client = await AuthClientAsync(clientUser.Email!);

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_NomeObrigatorio_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch(""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_TenantAdminDoProprioTenant_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var admin = await TestData.CreateUserAsync(sp, tenant.Id, Roles.TenantAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_TenantAdminDeOutroTenant_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var owner = await TestData.CreateTenantAsync(sp);
        var other = await TestData.CreateTenantAsync(sp);
        var adminOfOther = await TestData.CreateUserAsync(sp, other.Id, Roles.TenantAdmin);
        var client = await AuthClientAsync(adminOfOther.Email!);

        // A claim tenant_id do JWT é o `other`; a rota pede o `owner` → 403.
        var response = await client.PostAsJsonAsync(
            $"/api/tenants/{owner.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListarFiliais_Paginacao_RetornaPaginas()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        for (var i = 0; i < 3; i++)
        {
            var created = await client.PostAsJsonAsync(
                $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var page1 = await client.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenant.Id.Value}/branches?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(3, page1.TotalItems);
        Assert.All(page1.Items, b => Assert.Equal(tenant.Id.Value, b.TenantId));

        var page2 = await client.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenant.Id.Value}/branches?page=2&pageSize=2");
        Assert.Equal(2, page2!.Page);
        Assert.Single(page2.Items);
        Assert.All(page2.Items, b => Assert.Equal(tenant.Id.Value, b.TenantId));
    }

    [Fact]
    public async Task ListarFiliais_NaoVazaFiliaisDeOutroTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        await client.PostAsJsonAsync($"/api/tenants/{tenantA.Id.Value}/branches", NewBranch("Filial A1"));
        await client.PostAsJsonAsync($"/api/tenants/{tenantA.Id.Value}/branches", NewBranch("Filial A2"));
        await client.PostAsJsonAsync($"/api/tenants/{tenantB.Id.Value}/branches", NewBranch("Filial B1"));

        var listA = await client.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenantA.Id.Value}/branches");
        Assert.Equal(2, listA!.TotalItems);
        Assert.All(listA.Items, b => Assert.Equal(tenantA.Id.Value, b.TenantId));

        var listB = await client.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenantB.Id.Value}/branches");
        Assert.Equal(1, listB!.TotalItems);
        Assert.Equal("Filial B1", listB.Items[0].Name);
    }

    [Fact]
    public async Task ListarFiliais_TenantAdminSoVeOProprioTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var superClient = await AuthClientAsync(admin.Email!);

        await superClient.PostAsJsonAsync($"/api/tenants/{tenantA.Id.Value}/branches", NewBranch("Filial A1"));
        await superClient.PostAsJsonAsync($"/api/tenants/{tenantB.Id.Value}/branches", NewBranch("Filial B1"));

        var adminOfA = await TestData.CreateUserAsync(sp, tenantA.Id, Roles.TenantAdmin);
        var clientA = await AuthClientAsync(adminOfA.Email!);

        // Lista apenas o próprio tenant.
        var listA = await clientA.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenantA.Id.Value}/branches");
        Assert.Equal(1, listA!.TotalItems);
        Assert.All(listA.Items, b => Assert.Equal(tenantA.Id.Value, b.TenantId));

        // Acessar o tenant B é negado (claim tenant_id ≠ rota).
        var forbidden = await clientA.GetAsync($"/api/tenants/{tenantB.Id.Value}/branches");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ObterFilialPorId_Existente_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var created = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch("Filial X"));
        var branch = (await created.Content.ReadFromJsonAsync<BranchDto>())!;

        var response = await client.GetAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{branch.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fetched = await response.Content.ReadFromJsonAsync<BranchDto>();
        Assert.Equal(branch.Id, fetched!.Id);
        Assert.Equal("Filial X", fetched.Name);
    }

    [Fact]
    public async Task ObterFilialPorId_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.GetAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ObterFilial_DeOutroTenant_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        var created = await client.PostAsJsonAsync(
            $"/api/tenants/{tenantA.Id.Value}/branches", NewBranch());
        var branch = (await created.Content.ReadFromJsonAsync<BranchDto>())!;

        // Tentativa de ler uma filial de A sob o caminho de B → 404 (sem vazamento).
        var response = await client.GetAsync(
            $"/api/tenants/{tenantB.Id.Value}/branches/{branch.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AtualizarFilial_SuperAdmin_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var created = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch("Antiga"));
        var branch = (await created.Content.ReadFromJsonAsync<BranchDto>())!;

        var response = await client.PutAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{branch.Id}",
            UpdateBranch(tenant.Id.Value, branch.Id, "Nova"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<BranchDto>();
        Assert.Equal("Nova", updated!.Name);
        Assert.Equal("01310100", updated.Address.PostalCode);
        Assert.Equal("1130001122", updated.Contact.Phone);
    }

    [Fact]
    public async Task AtualizarFilial_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.PutAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{Guid.NewGuid()}",
            UpdateBranch(tenant.Id.Value, Guid.NewGuid(), "Nova"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletarFilial_SoftDelete_OcultaDaLista()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var created = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch("Para apagar"));
        var branch = (await created.Content.ReadFromJsonAsync<BranchDto>())!;

        var delete = await client.DeleteAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{branch.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await client.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenant.Id.Value}/branches");
        Assert.DoesNotContain(list!.Items, b => b.Id == branch.Id);

        var withInactive = await client.GetFromJsonAsync<PagedResult<BranchDto>>(
            $"/api/tenants/{tenant.Id.Value}/branches?includeInactive=true");
        var inactive = withInactive!.Items.Single(b => b.Id == branch.Id);
        Assert.False(inactive.IsActive);
        Assert.NotNull(inactive.DeletedAtUtc);

        var detail = await client.GetAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{branch.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Segundo delete → 404.
        var again = await client.DeleteAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{branch.Id}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task DeletarFilial_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.DeleteAsync(
            $"/api/tenants/{tenant.Id.Value}/branches/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Plan> CreateLinkedPlanAsync(
        IServiceProvider sp,
        HttpClient client,
        Tenant tenant,
        int? maxBranches,
        int? maxUsers = 10,
        int? maxStorageMb = 1024)
    {
        var module = await TestData.CreateModuleAsync(sp);
        var db = sp.GetRequiredService<IdentityDbContext>();
        var plan = Plan.Create(
            module.Id,
            $"Plano {Guid.NewGuid():N}",
            null,
            49.90m,
            499.00m,
            7,
            ["Dashboard"],
            maxBranches,
            maxUsers,
            maxStorageMb);
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var link = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/modules",
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);

        return plan;
    }

    [Fact]
    public async Task CriarFilial_LimiteDePlanoAtingido_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        // Plano com MaxBranches = 1.
        await CreateLinkedPlanAsync(sp, client, tenant, maxBranches: 1);

        var first = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Segunda filial excede o limite contratado → 400 (regra de negócio).
        var second = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task CriarFilial_LimiteNullSemLimite_NaoBloqueia()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        // null = "sem limite" (convenção da Etapa 18) → várias filiais passam.
        await CreateLinkedPlanAsync(sp, client, tenant, maxBranches: null);

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task CriarFilial_SomaDosLimitesDosPlanosAtivos()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        // Dois módulos ativos, cada plano com MaxBranches = 2 → efetivo 4.
        await CreateLinkedPlanAsync(sp, client, tenant, maxBranches: 2);
        await CreateLinkedPlanAsync(sp, client, tenant, maxBranches: 2);

        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // 5ª filial excede a soma (4) → 400.
        var exceeded = await client.PostAsJsonAsync(
            $"/api/tenants/{tenant.Id.Value}/branches", NewBranch());
        Assert.Equal(HttpStatusCode.BadRequest, exceeded.StatusCode);
    }
}
