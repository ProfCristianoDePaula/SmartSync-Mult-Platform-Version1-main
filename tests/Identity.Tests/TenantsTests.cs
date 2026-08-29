using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Application.Common;
using Identity.Application.Tenants;
using Identity.Domain.Common;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Identity.Tests.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// CRUD de tenants (Etapa 13): exclusivo SuperAdmin, unicidade GLOBAL de
/// documento (CPF/CNPJ) e e-mail (Etapa 04), tenant pode ser pessoa física ou
/// jurídica (Etapa 17), soft delete com query filter (oculto por padrão;
/// documento/e-mail liberados) e bloqueio de autenticação dos usuários de um
/// tenant soft-deletado. Módulos/planos do tenant são contratados pelos
/// endpoints de vínculo (Etapa 15), não mais no payload do tenant.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TenantsTests
{
    private readonly IdentityApiFactory _factory;

    public TenantsTests(IdentityApiFactory factory) => _factory = factory;

    private static CreateTenantCommand NewTenant(TipoPessoa tipo, string documento, string email)
        => new($"Razao Social {Guid.NewGuid():N}", $"Fantasia {Guid.NewGuid():N}", tipo, documento, email);

    private static CreateTenantCommand NewTenant(string documento, string email)
        => NewTenant(TipoPessoa.Juridica, documento, email);

    private async Task<HttpClient> AuthClientAsync(string email)
    {
        var tokens = await TestData.LoginAsync(_factory, email, TestData.UniquePassword());
        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    [Fact]
    public async Task CriarTenant_SuperAdmin_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var cnpj = TestData.UniqueCnpj();
        var response = await client.PostAsJsonAsync("/api/tenants", NewTenant(cnpj, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var tenant = await response.Content.ReadFromJsonAsync<TenantDto>();
        Assert.NotNull(tenant);
        Assert.Equal(cnpj, tenant!.Documento);
        Assert.Equal(TipoPessoa.Juridica, tenant.TipoPessoa);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.DeletedAtUtc);
    }

    [Fact]
    public async Task CriarTenant_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CriarTenant_Cliente_Retorna403()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var clientUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);
        var client = await AuthClientAsync(clientUser.Email!);

        var response = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CriarTenant_DocumentoObrigatorio_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.PostAsJsonAsync("/api/tenants",
            NewTenant("", TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarTenant_PessoaFisica_Retorna201()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var cpf = TestData.UniqueCpf();
        var response = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TipoPessoa.Fisica, cpf, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var tenant = await response.Content.ReadFromJsonAsync<TenantDto>();
        Assert.NotNull(tenant);
        Assert.Equal(TipoPessoa.Fisica, tenant!.TipoPessoa);
        Assert.Equal(cpf, tenant.Documento);
    }

    [Fact]
    public async Task CriarTenant_CpfComTipoJuridica_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        // CPF (11 dígitos) informado como pessoa jurídica: a validação do CNPJ
        // (14 dígitos) deve rejeitar, mesmo com dígitos verificadores válidos.
        var cpf = TestData.UniqueCpf();
        var response = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TipoPessoa.Juridica, cpf, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarTenant_CpfDuplicado_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var cpf = TestData.UniqueCpf();
        var first = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TipoPessoa.Fisica, cpf, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TipoPessoa.Fisica, cpf, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task CriarTenant_CnpjDuplicado_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var cnpj = TestData.UniqueCnpj();
        var first = await client.PostAsJsonAsync("/api/tenants", NewTenant(cnpj, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/tenants", NewTenant(cnpj, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task CriarTenant_EmailDuplicado_Retorna400()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var email = TestData.UniqueEmail("tenant");
        var first = await client.PostAsJsonAsync("/api/tenants", NewTenant(TestData.UniqueCnpj(), email));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/tenants", NewTenant(TestData.UniqueCnpj(), email));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ListarTenants_Paginacao_RetornaPaginas()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync("/api/tenants",
                NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));

        var page1 = await client.GetFromJsonAsync<PagedResult<TenantDto>>("/api/tenants?page=1&pageSize=2");
        Assert.NotNull(page1);
        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.True(page1.TotalItems >= 3);
        Assert.True(page1.TotalPages >= 2);

        var page2 = await client.GetFromJsonAsync<PagedResult<TenantDto>>("/api/tenants?page=2&pageSize=2");
        Assert.Equal(2, page2!.Page);
        Assert.True(page2.Items.Count >= 1);
    }

    [Fact]
    public async Task ListarTenants_FiltroPorDocumento_RetornaApenasOMatch()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var cnpj = TestData.UniqueCnpj();
        var created = await client.PostAsJsonAsync("/api/tenants", NewTenant(cnpj, TestData.UniqueEmail("tenant")));
        var target = (await created.Content.ReadFromJsonAsync<TenantDto>())!;
        await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));

        var result = await client.GetFromJsonAsync<PagedResult<TenantDto>>($"/api/tenants?documento={cnpj}");
        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalItems);
        Assert.Equal(target.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task ListarTenants_FiltroPorStatus_RetornaApenasStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var created = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));
        var target = (await created.Content.ReadFromJsonAsync<TenantDto>())!;

        // Desativa o tenant via PUT.
        var update = new UpdateTenantCommand(target.Id, target.LegalName, target.TradeName,
            target.Email, TenantStatus.Inactive);
        var put = await client.PutAsJsonAsync($"/api/tenants/{target.Id}", update);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var result = await client.GetFromJsonAsync<PagedResult<TenantDto>>("/api/tenants?status=Inactive");
        Assert.Contains(result!.Items, t => t.Id == target.Id);
        Assert.All(result.Items, t => Assert.Equal(TenantStatus.Inactive, t.Status));
    }

    [Fact]
    public async Task ObterTenantPorId_Existente_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var created = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));
        var tenant = (await created.Content.ReadFromJsonAsync<TenantDto>())!;

        var response = await client.GetAsync($"/api/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<TenantDto>();
        Assert.Equal(tenant.Id, fetched!.Id);
        Assert.Equal(tenant.LegalName, fetched.LegalName);
    }

    [Fact]
    public async Task ObterTenantPorId_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.GetAsync($"/api/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AtualizarTenant_SuperAdmin_Retorna200()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var created = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));
        var tenant = (await created.Content.ReadFromJsonAsync<TenantDto>())!;

        var update = new UpdateTenantCommand(
            tenant.Id, "Razao Editada", "Fantasia Editada",
            TestData.UniqueEmail("tenant"), TenantStatus.Active);

        var response = await client.PutAsJsonAsync($"/api/tenants/{tenant.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<TenantDto>();
        Assert.Equal("Razao Editada", updated!.LegalName);
        Assert.Equal(TenantStatus.Active, updated.Status);
        // O documento (CPF/CNPJ) é imutável.
        Assert.Equal(tenant.Documento, updated.Documento);
        Assert.Equal(tenant.TipoPessoa, updated.TipoPessoa);
    }

    [Fact]
    public async Task AtualizarTenant_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var update = new UpdateTenantCommand(
            Guid.NewGuid(), "Razao", "Fantasia", TestData.UniqueEmail("tenant"), TenantStatus.Active);
        var response = await client.PutAsJsonAsync($"/api/tenants/{update.Id}", update);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletarTenant_SoftDelete_OcultaDaListaECnpjFicaLivre()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var cnpj = TestData.UniqueCnpj();
        var created = await client.PostAsJsonAsync("/api/tenants", NewTenant(cnpj, TestData.UniqueEmail("tenant")));
        var tenant = (await created.Content.ReadFromJsonAsync<TenantDto>())!;

        var delete = await client.DeleteAsync($"/api/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var defaultList = await client.GetFromJsonAsync<PagedResult<TenantDto>>("/api/tenants");
        Assert.DoesNotContain(defaultList!.Items, t => t.Id == tenant.Id);

        var withInactive = await client.GetFromJsonAsync<PagedResult<TenantDto>>("/api/tenants?includeInactive=true");
        var inactive = withInactive!.Items.Single(t => t.Id == tenant.Id);
        Assert.False(inactive.IsActive);
        Assert.NotNull(inactive.DeletedAtUtc);

        var detail = await client.GetAsync($"/api/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Documento/e-mail ficam livres para um novo tenant ativo.
        var reuse = await client.PostAsJsonAsync("/api/tenants", NewTenant(cnpj, TestData.UniqueEmail("tenant")));
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
    }

    [Fact]
    public async Task DeletarTenant_Inexistente_Retorna404()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var response = await client.DeleteAsync($"/api/tenants/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantSoftDeletado_BloqueiaAutenticacaoDosUsuarios()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        // Tenant + usuário Client vinculado a ele.
        var created = await client.PostAsJsonAsync("/api/tenants",
            NewTenant(TestData.UniqueCnpj(), TestData.UniqueEmail("tenant")));
        var tenant = (await created.Content.ReadFromJsonAsync<TenantDto>())!;
        var tenantId = Identity.Domain.Common.TenantId.From(tenant.Id);
        var clientUser = await TestData.CreateUserAsync(sp, tenantId, Roles.Client);

        // Login do Client funciona enquanto o tenant está ativo.
        var loginOk = await TestData.LoginAsync(_factory, clientUser.Email!, TestData.UniquePassword());
        Assert.False(string.IsNullOrEmpty(loginOk.AccessToken));

        // Soft delete do tenant.
        var delete = await client.DeleteAsync($"/api/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Login passa a falhar (401) — o tenant não pode mais autenticar usuários.
        var loginClient = _factory.CreateApiClient();
        var loginFail = await loginClient.PostAsJsonAsync("/api/auth/login",
            TestData.Login(clientUser.Email!, TestData.UniquePassword()));
        Assert.Equal(HttpStatusCode.Unauthorized, loginFail.StatusCode);
    }

    [Fact]
    public async Task SoftDeleteTenant_RevogaRefreshTokensDosUsuariosDoTenant()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var admin = await TestData.CreateUserAsync(sp, tenantId: null, Roles.SuperAdmin);
        var client = await AuthClientAsync(admin.Email!);

        var tenant = await TestData.CreateTenantAsync(sp);
        var clientUser = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        // Login emite um refresh token ativo para o usuário do tenant.
        var tokens = await TestData.LoginAsync(_factory, clientUser.Email!, TestData.UniquePassword());
        var hash = TokenService.Hash(tokens.RefreshToken);

        var db = sp.GetRequiredService<IdentityDbContext>();
        var before = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash);
        Assert.Null(before.RevokedAtUtc);

        // Soft delete do tenant → revoga os refresh tokens dos usuários do tenant.
        var delete = await client.DeleteAsync($"/api/tenants/{tenant.Id.Value}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var readScope = _factory.Services.CreateScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var after = await readDb.RefreshTokens.AsNoTracking().SingleAsync(t => t.TokenHash == hash);
        Assert.NotNull(after.RevokedAtUtc);

        // O refresh do token revogado também falha (401).
        var refreshClient = _factory.CreateApiClient();
        var refresh = await refreshClient.PostAsJsonAsync("/api/auth/refresh-token",
            new Identity.Application.Auth.RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
