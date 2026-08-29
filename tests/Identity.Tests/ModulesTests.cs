using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common;
using Identity.Application.Modules;
using Identity.Domain.Common;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// CRUD do catálogo de módulos (Etapa 15): exclusivo SuperAdmin, slug/nome
/// únicos entre módulos ativos, slug imutável após a criação (é o identificador
/// que os microsserviços usam) e soft delete com query filter (módulos
/// inativos ocultos por padrão).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class ModulesTests
{
    private readonly IdentityApiFactory _factory;

    public ModulesTests(IdentityApiFactory factory) => _factory = factory;

    private static CreateModuleCommand NewModule(string name, string slug)
        => new(name, slug, "Módulo de teste");

    private async Task<HttpClient> AuthClientAsync(string email)
    {
        var tokens = await TestData.LoginAsync(_factory, email, TestData.UniquePassword());
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    [Fact]
    public async Task CriarModulo_SuperAdmin_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var slug = $"agro{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/modules", NewModule("SmartSync Agro", slug));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var module = await response.Content.ReadFromJsonAsync<ModuleDto>();
        Assert.NotNull(module);
        Assert.Equal("SmartSync Agro", module!.Name);
        Assert.Equal(slug, module.Slug);
        Assert.True(module.IsActive);
        Assert.Null(module.DeletedAtUtc);
    }

    [Fact]
    public async Task CriarModulo_SlugInvalido_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.PostAsJsonAsync("/api/modules", NewModule("Módulo", "Agro!")); // maiúscula + !
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarModulo_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/modules", NewModule("Módulo", "modulo"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CriarModulo_Cliente_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var clientUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var client = await AuthClientAsync(clientUser.Email!);

        var response = await client.PostAsJsonAsync("/api/modules", NewModule("Módulo", "modulo"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CriarModulo_SlugDuplicado_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var first = await client.PostAsJsonAsync("/api/modules", NewModule("Agro", "agro"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/modules", NewModule("Outro Agro", "AGRO"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task CriarModulo_NomeDuplicado_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var first = await client.PostAsJsonAsync("/api/modules", NewModule("SmartSync", "smart1"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/modules", NewModule("SmartSync", "smart2"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ListarModulos_Paginacao_RetornaTotalEPaginas()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var unique = Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/modules", NewModule($"QA {unique} A", $"qa{unique}a"));
        await client.PostAsJsonAsync("/api/modules", NewModule($"QA {unique} B", $"qa{unique}b"));
        await client.PostAsJsonAsync("/api/modules", NewModule($"QA {unique} C", $"qa{unique}c"));

        var page1 = await client.GetFromJsonAsync<PagedResult<ModuleDto>>("/api/modules?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.True(page1.TotalItems >= 3);
        Assert.True(page1.TotalPages >= 2);

        var page2 = await client.GetFromJsonAsync<PagedResult<ModuleDto>>("/api/modules?page=2&pageSize=2");
        Assert.Equal(2, page2!.Page);
        Assert.True(page2.Items.Count >= 1);
    }

    [Fact]
    public async Task ObterModuloPorId_Existente_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var created = await client.PostAsJsonAsync("/api/modules", NewModule("Módulo Único", "unico"));
        var module = (await created.Content.ReadFromJsonAsync<ModuleDto>())!;

        var response = await client.GetAsync($"/api/modules/{module.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<ModuleDto>();
        Assert.Equal(module.Id, fetched!.Id);
        Assert.Equal("Módulo Único", fetched.Name);
    }

    [Fact]
    public async Task ObterModuloPorId_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.GetAsync($"/api/modules/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AtualizarModulo_Retorna200_ComSlugImutavel()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var created = await client.PostAsJsonAsync("/api/modules", NewModule("Antes", "antes"));
        var module = (await created.Content.ReadFromJsonAsync<ModuleDto>())!;

        var update = new UpdateModuleCommand(module.Id, "Depois", "Nova descrição");
        var response = await client.PutAsJsonAsync($"/api/modules/{module.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ModuleDto>();
        Assert.Equal("Depois", updated!.Name);
        Assert.Equal("antes", updated.Slug); // slug não muda (identidade)
    }

    [Fact]
    public async Task AtualizarModulo_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.PutAsJsonAsync(
            $"/api/modules/{Guid.NewGuid()}", new UpdateModuleCommand(Guid.NewGuid(), "X", null));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletarModulo_SoftDelete_OcultaDaListaENomeFicaLivre()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var created = await client.PostAsJsonAsync("/api/modules", NewModule("Módulo Fim", "fim"));
        var module = (await created.Content.ReadFromJsonAsync<ModuleDto>())!;

        var delete = await client.DeleteAsync($"/api/modules/{module.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var defaultList = await client.GetFromJsonAsync<PagedResult<ModuleDto>>("/api/modules");
        Assert.DoesNotContain(defaultList!.Items, m => m.Id == module.Id);

        var withInactive = await client.GetFromJsonAsync<PagedResult<ModuleDto>>("/api/modules?includeInactive=true&pageSize=100");
        Assert.Contains(withInactive!.Items, m => m.Id == module.Id);
        Assert.False(withInactive.Items.Single(m => m.Id == module.Id).IsActive);
        Assert.NotNull(withInactive.Items.Single(m => m.Id == module.Id).DeletedAtUtc);

        var detail = await client.GetAsync($"/api/modules/{module.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Slug fica livre para um novo módulo ativo.
        var reuse = await client.PostAsJsonAsync("/api/modules", NewModule("Módulo Renascido", "fim"));
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task DeletarModulo_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.DeleteAsync($"/api/modules/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
