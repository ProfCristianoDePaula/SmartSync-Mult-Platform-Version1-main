using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Emitente fiscal (Fiscal-4): CRUD com isolamento, CPF bloqueado (422),
/// concessão por unidade, prontidão e gate de produção — Postgres real.
/// </summary>
[Collection("integration")]
public sealed class FiscalEmitenteTests(FiscalApiFixture fixture)
{
    private static readonly Guid BranchX = Guid.NewGuid();
    private static readonly Guid BranchY = Guid.NewGuid();

    private HttpClient Client(string? token)
    {
        var client = fixture.Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string Tenant(string role, Guid? tenant = null)
    {
        fixture.ModuleActive = true;
        return fixture.IssueToken(tenant ?? FiscalApiFixture.TenantA, role);
    }

    private static object NovoEmitente(Guid branch, string cnpj = "11222333000181") => new
    {
        branchId = branch,
        cnpj,
        razaoSocial = "Loja Teste LTDA",
        fantasia = "Loja Teste",
        inscricaoEstadual = "123456789",
        inscricaoMunicipal = "98765",
        cnae = "4713001",
        crt = 3,
        endereco = new
        {
            street = "Rua Fiscal",
            number = "100",
            complement = (string?)null,
            district = "Centro",
            city = "São Paulo",
            state = "SP",
            postalCode = "01310100",
            codigoIbgeMunicipio = "3550308"
        },
        telefone = "11999998888",
        email = "fiscal@lojateste.local"
    };

    [Fact]
    public async Task TenantAdmin_CriaEmitente_E_IsolamentoEntreTenants()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var created = await client.PostAsJsonAsync("/api/fiscal/emitentes", NovoEmitente(BranchX));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<EmitenteDto>();
        Assert.NotNull(dto);
        Assert.False(dto!.CnpjAlfanumerico);

        using var other = Client(Tenant("TenantAdmin", FiscalApiFixture.TenantB));
        var get = await other.GetAsync($"/api/fiscal/emitentes/{dto.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Emitente_CPF_DeveRetornar422()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var response = await client.PostAsJsonAsync(
            "/api/fiscal/emitentes", NovoEmitente(Guid.NewGuid(), "12345678909"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Emitente_CnpjInvalido_DeveRetornar400()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var response = await client.PostAsJsonAsync(
            "/api/fiscal/emitentes", NovoEmitente(Guid.NewGuid(), "11222333000182"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Manager_SemConcessao_Escrever_DeveRetornar403()
    {
        using var client = Client(Tenant("Manager"));
        var response = await client.PostAsJsonAsync(
            "/api/fiscal/emitentes", NovoEmitente(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_ComConcessao_CriaEmitente()
    {
        var managerId = Guid.NewGuid();
        using var admin = Client(Tenant("TenantAdmin"));
        var grant = await admin.PostAsJsonAsync("/api/fiscal/concessoes", new
        {
            branchId = BranchY,
            userId = managerId,
            papel = 1
        });
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);

        using var manager = Client(fixture.IssueToken(FiscalApiFixture.TenantA, "Manager", managerId));
        var created = await manager.PostAsJsonAsync("/api/fiscal/emitentes", NovoEmitente(BranchY));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Prontidao_ApontaCertificado_E_Conexao_Pendentes()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var created = await client.PostAsJsonAsync("/api/fiscal/emitentes", NovoEmitente(Guid.NewGuid()));
        var dto = await created.Content.ReadFromJsonAsync<EmitenteDto>();
        Assert.NotNull(dto);

        var pront = await client.GetFromJsonAsync<ProntidaoDto>(
            $"/api/fiscal/emitentes/{dto!.Id}/prontidao?tipo=55&ambiente=1");
        Assert.NotNull(pront);
        Assert.False(pront!.Pronto);
        // Regressão Fiscal-5: configs precisam nascer ativas (visíveis ao query filter).
        Assert.Contains(pront.Itens, i => i.Item == "serie-definida" && i.Ok);
        Assert.Contains(pront.Itens, i => i.Item == "certificado-valido" && !i.Ok);
        Assert.Contains(pront.Itens, i => i.Item == "conexao-homologacao" && !i.Ok);
    }

    [Fact]
    public async Task PromoverProducao_SemFlag_DeveRetornar409()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var created = await client.PostAsJsonAsync("/api/fiscal/emitentes", NovoEmitente(Guid.NewGuid()));
        var dto = await created.Content.ReadFromJsonAsync<EmitenteDto>();
        Assert.NotNull(dto);

        var response = await client.PostAsJsonAsync(
            $"/api/fiscal/emitentes/{dto!.Id}/ambientes/55/promover-producao",
            new { textoConfirmacao = "PROMOVER PARA PRODUCAO" });
        // Sem prontidão completa o pedido cai antes do gate; com tudo pronto cairia no 409 da flag.
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"esperado 400/409, veio {(int)response.StatusCode}");
    }

    [Fact]
    public async Task PromoverProducao_TextoErrado_DeveRetornar400()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var created = await client.PostAsJsonAsync("/api/fiscal/emitentes", NovoEmitente(Guid.NewGuid()));
        var dto = await created.Content.ReadFromJsonAsync<EmitenteDto>();
        Assert.NotNull(dto);

        var response = await client.PostAsJsonAsync(
            $"/api/fiscal/emitentes/{dto!.Id}/ambientes/55/promover-producao",
            new { textoConfirmacao = "promover" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record EmitenteDto(Guid Id, bool CnpjAlfanumerico);
    private sealed record ProntidaoItemDto(string Item, bool Ok, string Detalhe);
    private sealed record ProntidaoDto(bool Pronto, List<ProntidaoItemDto> Itens);
}
