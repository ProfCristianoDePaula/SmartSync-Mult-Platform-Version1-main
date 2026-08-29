using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Domain.Common;
using Identity.Tests.Lab;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Tests;

/// <summary>
/// Catálogo de roles (Etapa 11/12): GET /api/roles exige autenticação e
/// devolve as roles da plataforma na ordem canônica.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class RolesTests
{
    private readonly IdentityApiFactory _factory;

    public RolesTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ListarRoles_SemToken_Retorna401()
    {
        var client = _factory.CreateApiClient();
        var response = await client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListarRoles_Autenticado_RetornaRolesDaPlataforma()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var tenant = await TestData.CreateTenantAsync(sp);
        var user = await TestData.CreateUserAsync(sp, tenant.Id, Roles.Client);

        var tokens = await TestData.LoginAsync(_factory, user.Email!, TestData.UniquePassword());

        var client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var roles = (await response.Content.ReadFromJsonAsync<List<string>>())!;
        Assert.Equal(Roles.All, roles);
    }
}
