using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common;
using Identity.Application.Plans;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// CRUD do catálogo de planos por MÓDULO (Etapa 15): rotas aninhadas em
/// <c>/api/modules/{moduleId}/plans</c>, exclusivo SuperAdmin, validações via
/// FluentValidation, nome único POR MÓDULO entre planos ativos e soft delete
/// com query filter (planos inativos ocultos por padrão).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class PlansTests
{
    private readonly IdentityApiFactory _factory;

    public PlansTests(IdentityApiFactory factory) => _factory = factory;

    private static CreatePlanCommand NewPlan(string name) => new(
        Guid.Empty,
        name,
        "Plano de teste",
        49.90m,
        499.00m,
        7,
        ["Dashboard", "Relatórios"],
        5,
        10,
        1024);

    private async Task<HttpClient> AuthClientAsync(string email)
    {
        var tokens = await TestData.LoginAsync(_factory, email, TestData.UniquePassword());
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static string ModuleUrl(Guid moduleId) => $"/api/modules/{moduleId}/plans";

    [Fact]
    public async Task CriarPlano_SuperAdmin_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);

        var response = await client.PostAsJsonAsync(ModuleUrl(module.Id.Value), NewPlan("Plano Básico"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanDto>();
        Assert.NotNull(plan);
        Assert.Equal("Plano Básico", plan!.Name);
        Assert.Equal(module.Id.Value, plan.ModuleId);
        Assert.Equal(49.90m, plan.MonthlyPrice);
        Assert.Equal(499.00m, plan.AnnualPrice);
        Assert.Equal(7, plan.TrialDays);
        Assert.Equal(2, plan.Features.Count);
        Assert.Equal<int?>(5, plan.MaxBranches);
        Assert.Equal<int?>(10, plan.MaxUsers);
        Assert.Equal<int?>(1024, plan.MaxStorageMb);
        Assert.True(plan.IsActive);
        Assert.Null(plan.DeletedAtUtc);
    }

    [Fact]
    public async Task CriarPlano_ModuloInexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.PostAsJsonAsync(ModuleUrl(Guid.NewGuid()), NewPlan("Plano Órfão"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CriarPlano_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync(ModuleUrl(Guid.NewGuid()), NewPlan("Plano Anônimo"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CriarPlano_Cliente_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var clientUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var client = await AuthClientAsync(clientUser.Email!);

        var response = await client.PostAsJsonAsync(ModuleUrl(Guid.NewGuid()), NewPlan("Plano de Client"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CriarPlano_NomeObrigatorio_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);

        var response = await client.PostAsJsonAsync(ModuleUrl(module.Id.Value), NewPlan(""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarPlano_NomeDuplicadoNoMesmoModulo_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);

        var first = await client.PostAsJsonAsync(ModuleUrl(module.Id.Value), NewPlan("Plano Pro"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(ModuleUrl(module.Id.Value), NewPlan("plano pro"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task CriarPlano_MesmoNomeEmModulosDiferentes_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var moduleA = await TestData.CreateModuleAsync(sp);
        var moduleB = await TestData.CreateModuleAsync(sp);

        // Unicidade é POR MÓDULO (Etapa 15): o mesmo nome pode existir em
        // módulos diferentes.
        var a = await client.PostAsJsonAsync(ModuleUrl(moduleA.Id.Value), NewPlan("Plano Compartilhado"));
        Assert.Equal(HttpStatusCode.Created, a.StatusCode);

        var b = await client.PostAsJsonAsync(ModuleUrl(moduleB.Id.Value), NewPlan("Plano Compartilhado"));
        Assert.Equal(HttpStatusCode.Created, b.StatusCode);
    }

    [Fact]
    public async Task ListarPlanos_Paginacao_RetornaTotalEPaginas()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var moduleUrl = ModuleUrl(module.Id.Value);

        // Nomes com prefixo único por teste (o Postgres é compartilhado pela
        // suíte); as asserções de paginação são relativas, não absolutas.
        var unique = Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync(moduleUrl, NewPlan($"QA {unique} A"));
        await client.PostAsJsonAsync(moduleUrl, NewPlan($"QA {unique} B"));
        await client.PostAsJsonAsync(moduleUrl, NewPlan($"QA {unique} C"));

        var page1 = await client.GetFromJsonAsync<PagedResult<PlanDto>>($"{moduleUrl}?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.True(page1.TotalItems >= 3);
        Assert.True(page1.TotalPages >= 2);

        var page2 = await client.GetFromJsonAsync<PagedResult<PlanDto>>($"{moduleUrl}?page=2&pageSize=2");
        Assert.NotNull(page2);
        Assert.Equal(2, page2!.Page);
        Assert.True(page2.Items.Count >= 1);
    }

    [Fact]
    public async Task ListarPlanos_NaoAtravessaModulos()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var moduleA = await TestData.CreateModuleAsync(sp);
        var moduleB = await TestData.CreateModuleAsync(sp);

        var unique = Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync(ModuleUrl(moduleA.Id.Value), NewPlan($"{unique} Do A"));
        await client.PostAsJsonAsync(ModuleUrl(moduleB.Id.Value), NewPlan($"{unique} Do B"));

        // A listagem do módulo A não contém planos do módulo B.
        var listA = await client.GetFromJsonAsync<PagedResult<PlanDto>>(ModuleUrl(moduleA.Id.Value));
        Assert.All(listA!.Items, p => Assert.Equal(moduleA.Id.Value, p.ModuleId));
        Assert.Contains(listA.Items, p => p.Name == $"{unique} Do A");
        Assert.DoesNotContain(listA.Items, p => p.Name == $"{unique} Do B");
    }

    [Fact]
    public async Task ObterPlanoPorId_Existente_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var moduleUrl = ModuleUrl(module.Id.Value);

        var created = await client.PostAsJsonAsync(moduleUrl, NewPlan("Plano Único"));
        var plan = (await created.Content.ReadFromJsonAsync<PlanDto>())!;

        var response = await client.GetAsync($"{moduleUrl}/{plan.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<PlanDto>();
        Assert.Equal(plan.Id, fetched!.Id);
        Assert.Equal("Plano Único", fetched.Name);
    }

    [Fact]
    public async Task ObterPlanoPorId_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);

        var response = await client.GetAsync($"{ModuleUrl(module.Id.Value)}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AtualizarPlano_SuperAdmin_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var moduleUrl = ModuleUrl(module.Id.Value);

        var created = await client.PostAsJsonAsync(moduleUrl, NewPlan("Plano Editável"));
        var plan = (await created.Content.ReadFromJsonAsync<PlanDto>())!;

        var update = new UpdatePlanCommand(
            module.Id.Value, plan.Id, "Plano Editado", "Nova descrição", 59.90m, 599.00m, 14,
            ["Dashboard", "Relatórios", "API"], 10, 20, 2048);

        var response = await client.PutAsJsonAsync($"{moduleUrl}/{plan.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<PlanDto>();
        Assert.Equal("Plano Editado", updated!.Name);
        Assert.Equal(59.90m, updated.MonthlyPrice);
        Assert.Equal(3, updated.Features.Count);
        Assert.Equal<int?>(20, updated.MaxUsers);
    }

    [Fact]
    public async Task AtualizarPlano_NomeDuplicadoNoMesmoModulo_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var moduleUrl = ModuleUrl(module.Id.Value);

        var a = await client.PostAsJsonAsync(moduleUrl, NewPlan("Plano X"));
        var b = await client.PostAsJsonAsync(moduleUrl, NewPlan("Plano Y"));
        var planB = (await b.Content.ReadFromJsonAsync<PlanDto>())!;

        // Renomeia o plano B para um nome já usado pelo plano A ativo.
        var response = await client.PutAsJsonAsync($"{moduleUrl}/{planB.Id}",
            new UpdatePlanCommand(module.Id.Value, planB.Id, "Plano X", null, 1, 1, 0, [], 1, 1, 1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AtualizarPlano_DeOutroModulo_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var moduleA = await TestData.CreateModuleAsync(sp);
        var moduleB = await TestData.CreateModuleAsync(sp);

        var created = await client.PostAsJsonAsync(ModuleUrl(moduleA.Id.Value), NewPlan("Plano do Módulo A"));
        var plan = (await created.Content.ReadFromJsonAsync<PlanDto>())!;

        // Tentar editar o plano A pela rota do módulo B → 404 (a query é
        // escopada ao módulo da rota).
        var response = await client.PutAsJsonAsync($"{ModuleUrl(moduleB.Id.Value)}/{plan.Id}",
            new UpdatePlanCommand(moduleB.Id.Value, plan.Id, "Outro nome", null, 1, 1, 0, [], 1, 1, 1));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletarPlano_SoftDelete_OcultaDaListaENomeFicaLivre()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var moduleUrl = ModuleUrl(module.Id.Value);

        var created = await client.PostAsJsonAsync(moduleUrl, NewPlan("Plano Fim"));
        var plan = (await created.Content.ReadFromJsonAsync<PlanDto>())!;

        // Soft delete → 204.
        var delete = await client.DeleteAsync($"{moduleUrl}/{plan.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Fora das consultas por padrão; aparece com includeInactive.
        var defaultList = await client.GetFromJsonAsync<PagedResult<PlanDto>>(moduleUrl);
        Assert.DoesNotContain(defaultList!.Items, p => p.Id == plan.Id);

        var withInactive = await client.GetFromJsonAsync<PagedResult<PlanDto>>($"{moduleUrl}?includeInactive=true&pageSize=100");
        Assert.Contains(withInactive!.Items, p => p.Id == plan.Id);
        Assert.False(withInactive.Items.Single(p => p.Id == plan.Id).IsActive);
        Assert.NotNull(withInactive.Items.Single(p => p.Id == plan.Id).DeletedAtUtc);

        // Detalhe do plano inativo → 404.
        var detail = await client.GetAsync($"{moduleUrl}/{plan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Nome fica livre para um novo plano ativo.
        var reuse = await client.PostAsJsonAsync(moduleUrl, NewPlan("Plano Fim"));
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task DeletarPlano_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);

        var response = await client.DeleteAsync($"{ModuleUrl(module.Id.Value)}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
