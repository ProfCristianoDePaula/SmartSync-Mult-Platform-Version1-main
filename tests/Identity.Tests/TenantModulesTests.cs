using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common;
using Identity.Application.TenantModules;
using Identity.Domain.Common;
using Identity.Domain.Enums;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Vínculos tenant↔módulo (Etapa 15): rotas em /api/tenants/{tenantId}/modules.
/// Exclusivo SuperAdmin (decisão do usuário — TenantAdmin NÃO opera vínculos,
/// nem no próprio tenant, por ser contratação/billing). Regras: um vínculo ATIVO
/// por (tenant, module); troca de plano inativa a vigência atual e abre uma nova
/// (histórico preservado); mesmo plano é idempotente; desvincular encerra a
/// vigência ativa. Módulos/planos nunca atravessam tenants/módulos.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TenantModulesTests
{
    private readonly IdentityApiFactory _factory;

    public TenantModulesTests(IdentityApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthClientAsync(string email)
    {
        var tokens = await TestData.LoginAsync(_factory, email, TestData.UniquePassword());
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static string TenantModulesUrl(Guid tenantId) => $"/api/tenants/{tenantId}/modules";

    [Fact]
    public async Task Vincular_SuperAdmin_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var link = await response.Content.ReadFromJsonAsync<TenantModuleDto>();
        Assert.NotNull(link);
        Assert.Equal(tenant.Id.Value, link!.TenantId);
        Assert.Equal(module.Id.Value, link.ModuleId);
        Assert.Equal(module.Name, link.ModuleName);
        Assert.Equal(plan.Id.Value, link.PlanId);
        Assert.Equal(TenantModuleStatus.Active, link.Status);
        Assert.Null(link.EndDateUtc);
    }

    [Fact]
    public async Task Vincular_TenantInexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(Guid.NewGuid()),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vincular_ModuloInexistente_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Vincular_PlanoDeOutroModulo_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var moduleA = await TestData.CreateModuleAsync(sp);
        var moduleB = await TestData.CreateModuleAsync(sp);
        var planB = await TestData.CreatePlanAsync(sp, moduleB.Id);

        // Vincula o módulo A usando um plano do módulo B → 400 (regra de negócio).
        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, moduleA.Id.Value, planB.Id.Value));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Vincular_VinculoAtivoExistente_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var first = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Vincular_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(Guid.NewGuid()),
            new LinkTenantModuleCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Vincular_TenantAdmin_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var tenantAdmin = await TestData.CreateUserAsync(sp, tenant.Id, Roles.TenantAdmin);
        var client = await AuthClientAsync(tenantAdmin.Email!);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        // Decisão da Etapa 15: TenantAdmin não opera vínculos, nem no próprio
        // tenant (contratação/billing é sensível) → 403.
        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Vincular_Cliente_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var clientUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var client = await AuthClientAsync(clientUser.Email!);

        var response = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TrocarPlano_Diferente_InativaVigenciaAtualEIniciaNova()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);
        var planBasic = await TestData.CreatePlanAsync(sp, module.Id, "Básico");
        var planPro = await TestData.CreatePlanAsync(sp, module.Id, "Pro");

        var created = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, planBasic.Id.Value));
        var original = (await created.Content.ReadFromJsonAsync<TenantModuleDto>())!;

        // Upgrade para "Pro": inativa a vigência atual e abre uma nova.
        var response = await client.PutAsJsonAsync(
            $"{TenantModulesUrl(tenant.Id.Value)}/{module.Id.Value}",
            new UpdateTenantModuleCommand(Guid.Empty, module.Id.Value, planPro.Id.Value));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var upgraded = await response.Content.ReadFromJsonAsync<TenantModuleDto>();
        Assert.Equal(planPro.Id.Value, upgraded!.PlanId);
        Assert.Equal(TenantModuleStatus.Active, upgraded.Status);
        Assert.Null(upgraded.EndDateUtc);
        Assert.NotEqual(original.Id, upgraded.Id); // nova vigência

        // Histórico preservado: a vigência antiga aparece com includeInactive.
        var history = await client.GetFromJsonAsync<PagedResult<TenantModuleDto>>(
            $"{TenantModulesUrl(tenant.Id.Value)}?includeInactive=true");
        Assert.Contains(history!.Items, l => l.Id == original.Id && l.Status == TenantModuleStatus.Inactive && l.EndDateUtc != null);
        Assert.Contains(history.Items, l => l.Id == upgraded.Id && l.Status == TenantModuleStatus.Active);
    }

    [Fact]
    public async Task TrocarPlano_MesmoPlano_Idempotente()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var created = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        var original = (await created.Content.ReadFromJsonAsync<TenantModuleDto>())!;

        // Mesmo plano: sem nova vigência, devolve o vínculo atual.
        var response = await client.PutAsJsonAsync(
            $"{TenantModulesUrl(tenant.Id.Value)}/{module.Id.Value}",
            new UpdateTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var same = await response.Content.ReadFromJsonAsync<TenantModuleDto>();
        Assert.Equal(original.Id, same!.Id);

        var list = await client.GetFromJsonAsync<PagedResult<TenantModuleDto>>(TenantModulesUrl(tenant.Id.Value));
        Assert.Equal(1, list!.Items.Count(l => l.ModuleId == module.Id.Value && l.Status == TenantModuleStatus.Active));
    }

    [Fact]
    public async Task TrocarPlano_SemVinculoAtivo_CriaNovo_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        // PUT direto (reativação): não há vínculo ativo → cria um.
        var response = await client.PutAsJsonAsync(
            $"{TenantModulesUrl(tenant.Id.Value)}/{module.Id.Value}",
            new UpdateTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var link = await response.Content.ReadFromJsonAsync<TenantModuleDto>();
        Assert.Equal(TenantModuleStatus.Active, link!.Status);
    }

    [Fact]
    public async Task Desvincular_Retorna204_EncerraVigenciaAtiva()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var created = await client.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        var link = (await created.Content.ReadFromJsonAsync<TenantModuleDto>())!;

        var unlink = await client.DeleteAsync($"{TenantModulesUrl(tenant.Id.Value)}/{module.Id.Value}");
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);

        // Fora da listagem padrão (só ativos).
        var defaultList = await client.GetFromJsonAsync<PagedResult<TenantModuleDto>>(TenantModulesUrl(tenant.Id.Value));
        Assert.DoesNotContain(defaultList!.Items, l => l.Id == link.Id);

        // Histórico com status inativo e data de fim.
        var history = await client.GetFromJsonAsync<PagedResult<TenantModuleDto>>(
            $"{TenantModulesUrl(tenant.Id.Value)}?includeInactive=true");
        var inactive = history!.Items.Single(l => l.Id == link.Id);
        Assert.Equal(TenantModuleStatus.Inactive, inactive.Status);
        Assert.NotNull(inactive.EndDateUtc);
    }

    [Fact]
    public async Task Desvincular_SemVinculoAtivo_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        var response = await client.DeleteAsync($"{TenantModulesUrl(tenant.Id.Value)}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Listar_Paginacao_RetornaTotalEPaginas()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);

        for (var i = 0; i < 3; i++)
        {
            var module = await TestData.CreateModuleAsync(sp);
            var plan = await TestData.CreatePlanAsync(sp, module.Id);
            var created = await client.PostAsJsonAsync(
                TenantModulesUrl(tenant.Id.Value),
                new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var page1 = await client.GetFromJsonAsync<PagedResult<TenantModuleDto>>(
            $"{TenantModulesUrl(tenant.Id.Value)}?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.True(page1.TotalItems >= 3);
        Assert.True(page1.TotalPages >= 2);
    }

    [Fact]
    public async Task Listar_NaoAtravessaTenants()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        var module = await TestData.CreateModuleAsync(sp);
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var forA = await client.PostAsJsonAsync(
            TenantModulesUrl(tenantA.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, forA.StatusCode);

        // A listagem do tenant B não contém vínculos do tenant A.
        var listB = await client.GetFromJsonAsync<PagedResult<TenantModuleDto>>(TenantModulesUrl(tenantB.Id.Value));
        Assert.Equal(0, listB!.TotalItems);
    }

    [Fact]
    public async Task ConsultarMeusModulos_Retorna200ComSlugENomeDoPlano()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var adminClient = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var slug = $"agro{Guid.NewGuid():N}";
        var module = await TestData.CreateModuleAsync(sp, name: $"SmartSync Agro {Guid.NewGuid():N}", slug: slug);
        var plan = await TestData.CreatePlanAsync(sp, module.Id, name: "Essencial");

        var link = await adminClient.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);

        var tenantAdmin = await TestData.CreateUserAsync(sp, tenant.Id, Roles.TenantAdmin);
        var client = await AuthClientAsync(tenantAdmin.Email!);

        var response = await client.GetFromJsonAsync<TenantModulesView>("/api/tenants/me/modules");
        Assert.NotNull(response);

        var item = Assert.Single(response!.Items);
        Assert.Equal(slug, item.Module);
        Assert.Equal(module.Name, item.Name);
        Assert.Equal("Essencial", item.Plan);
        Assert.Equal("active", item.Status);
        Assert.Null(item.EndDateUtc);
    }

    [Fact]
    public async Task ConsultarMeusModulos_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/api/tenants/me/modules");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConsultarMeusModulos_SuperAdminGlobalSemClaim_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        // SuperAdmin global não tem claim tenant_id → não há "meu tenant".
        var response = await client.GetAsync("/api/tenants/me/modules");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ConsultarMeusModulos_TenantAdminVeApenasModulosDoProprioTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var adminClient = await AuthClientAsync(admin.Email!);
        var tenantA = await TestData.CreateTenantAsync(sp);
        var tenantB = await TestData.CreateTenantAsync(sp);

        var module = await TestData.CreateModuleAsync(sp, slug: $"agro{Guid.NewGuid():N}");
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var link = await adminClient.PostAsJsonAsync(
            TenantModulesUrl(tenantA.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);

        var tenantAdminB = await TestData.CreateUserAsync(sp, tenantB.Id, Roles.TenantAdmin);
        var clientB = await AuthClientAsync(tenantAdminB.Email!);

        var response = await clientB.GetFromJsonAsync<TenantModulesView>("/api/tenants/me/modules");
        Assert.NotNull(response);
        Assert.Empty(response!.Items);
    }

    [Fact]
    public async Task ConsultarMeusModulos_ModuloSoftDeletado_NaoAparece()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var adminClient = await AuthClientAsync(admin.Email!);
        var tenant = await TestData.CreateTenantAsync(sp);
        var module = await TestData.CreateModuleAsync(sp, slug: $"agenda{Guid.NewGuid():N}");
        var plan = await TestData.CreatePlanAsync(sp, module.Id);

        var link = await adminClient.PostAsJsonAsync(
            TenantModulesUrl(tenant.Id.Value),
            new LinkTenantModuleCommand(Guid.Empty, module.Id.Value, plan.Id.Value));
        Assert.Equal(HttpStatusCode.Created, link.StatusCode);

        var del = await adminClient.DeleteAsync($"/api/modules/{module.Id.Value}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var tenantAdmin = await TestData.CreateUserAsync(sp, tenant.Id, Roles.TenantAdmin);
        var client = await AuthClientAsync(tenantAdmin.Email!);

        var response = await client.GetFromJsonAsync<TenantModulesView>("/api/tenants/me/modules");
        Assert.NotNull(response);
        Assert.Empty(response!.Items);
    }
}
