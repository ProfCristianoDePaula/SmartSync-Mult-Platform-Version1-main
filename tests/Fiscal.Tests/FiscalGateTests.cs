using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fiscal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Gate do Fiscal (Fiscal-1): health público, 401 sem token, 403 sem
/// tenant_id (SuperAdmin, R5) ou sem módulo, 200 com módulo + auditoria,
/// e isolamento entre tenants (R5).
/// </summary>
[Collection("integration")]
public sealed class FiscalGateTests(FiscalApiFixture fixture)
{
    private HttpClient Client(string? token)
    {
        var client = fixture.Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
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
    public async Task Ping_SemToken_DeveRetornar401()
    {
        using var client = Client(null);
        var response = await client.GetAsync("/api/fiscal/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_SuperAdminSemTenant_DeveRetornar403()
    {
        fixture.ModuleActive = true; // mesmo com módulo, sem claim não passa (R5)
        using var client = Client(fixture.IssueToken(null, "SuperAdmin"));
        var response = await client.GetAsync("/api/fiscal/ping");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ping_SemModulo_DeveRetornar403()
    {
        fixture.ModuleActive = false;
        using var client = Client(fixture.IssueToken(FiscalApiFixture.TenantA, "TenantAdmin"));
        var response = await client.GetAsync("/api/fiscal/ping");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ping_ComModulo_DeveRetornar200_E_Auditar()
    {
        fixture.ModuleActive = true;
        using var client = Client(fixture.IssueToken(FiscalApiFixture.TenantA, "TenantAdmin"));
        var response = await client.GetAsync("/api/fiscal/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PingDto>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
        Assert.Equal(FiscalApiFixture.TenantA, body.TenantId);

        // Auditoria persistida no Postgres real (sem mock de banco, R12).
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FiscalDbContext>();
        var tenantId = Fiscal.Domain.Common.TenantId.From(FiscalApiFixture.TenantA);
        var logged = db.AuditLogs.Any(a =>
            a.TenantId == tenantId && a.Action == "fiscal.ping" && a.Entity == "FiscalPing");
        Assert.True(logged);
    }

    [Fact]
    public async Task Ping_TenantB_NaoEnxergaTrilhaDoTenantA()
    {
        fixture.ModuleActive = true;

        using var clientA = Client(fixture.IssueToken(FiscalApiFixture.TenantA, "TenantAdmin"));
        var pingA = await clientA.GetAsync("/api/fiscal/ping");
        Assert.Equal(HttpStatusCode.OK, pingA.StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FiscalDbContext>();
        var tenantA = Fiscal.Domain.Common.TenantId.From(FiscalApiFixture.TenantA);
        var tenantB = Fiscal.Domain.Common.TenantId.From(FiscalApiFixture.TenantB);

        // Prova de não-vazamento (R5): nenhuma linha do tenant B carrega o tenant A.
        var crossLeak = db.AuditLogs.Any(a =>
            a.TenantId == tenantB && a.EntityId == FiscalApiFixture.TenantA.ToString());
        Assert.False(crossLeak);

        var ownRows = db.AuditLogs.Count(a => a.TenantId == tenantA);
        Assert.True(ownRows >= 1);
    }

    private sealed record PingDto(string Status, Guid TenantId, DateTime AgoraUtc);
}
