using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Catálogo fiscal (Fiscal-2): seed de 27 UFs, CRUD SuperAdmin, leitura do
/// tenant, validação de host, unicidade e importação idempotente — tudo em
/// Postgres real (R12). Nenhuma URL inventada: o seed vem de fontes oficiais
/// (R3); o teste usa hosts .gov.br fictícios isolados no banco de teste.
/// </summary>
[Collection("integration")]
public sealed class FiscalCatalogTests(FiscalApiFixture fixture)
{
    private HttpClient Client(string? token)
    {
        var client = fixture.Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string SuperAdmin() => fixture.IssueToken(null, "SuperAdmin");

    private string Tenant(string role = "TenantAdmin")
    {
        fixture.ModuleActive = true;
        return fixture.IssueToken(FiscalApiFixture.TenantA, role);
    }

    [Fact]
    public async Task Seed_DeveConter27UFs_E_SP_ComCuf35()
    {
        using var client = Client(Tenant());
        var ufs = await client.GetFromJsonAsync<List<UfDto>>("/api/fiscal/ufs");
        Assert.NotNull(ufs);
        Assert.Equal(27, ufs!.Count);
        var sp = ufs.Single(u => u.Sigla == "SP");
        Assert.Equal(35, sp.CodigoIbge);
        Assert.Equal("São Paulo", sp.Nome);
    }

    [Fact]
    public async Task SuperAdmin_CriarUfDuplicada_DeveRetornar400()
    {
        using var client = Client(SuperAdmin());
        var response = await client.PostAsJsonAsync("/api/fiscal/ufs", new
        {
            sigla = "SP",
            codigoIbge = 35,
            nome = "São Paulo",
            autorizadorNFe = 1,
            autorizadorNFCe = 1
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TenantAdmin_EscreverUf_DeveRetornar403()
    {
        using var client = Client(Tenant("TenantAdmin"));
        var response = await client.PostAsJsonAsync("/api/fiscal/ufs", new
        {
            sigla = "XX",
            codigoIbge = 99,
            nome = "Inexistente",
            autorizadorNFe = 1,
            autorizadorNFCe = 1
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CriarEndpointHostInvalido_DeveRetornar400()
    {
        using var client = Client(SuperAdmin());
        var response = await client.PostAsJsonAsync("/api/fiscal/sefaz-endpoints", new
        {
            autorizador = "XX",
            modelo = 55,
            servico = 1,
            ambiente = 1,
            versaoServico = "4.00",
            url = "https://example.com/sefaz/status",
            fonteUrl = "https://www.nfe.fazenda.gov.br/portal/webServices.aspx"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CriarEndpointDuplicado_DeveRetornar400()
    {
        using var client = Client(SuperAdmin());
        var payload = new
        {
            autorizador = "ZZ",
            modelo = 55,
            servico = 1,
            ambiente = 1,
            versaoServico = "4.00",
            url = "https://homologacao.zz.gov.br/ws/status",
            fonteUrl = "https://www.nfe.fazenda.gov.br/portal/webServices.aspx"
        };

        var first = await client.PostAsJsonAsync("/api/fiscal/sefaz-endpoints", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/fiscal/sefaz-endpoints", payload);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Importacao_Idempotente_ComLinhaRejeitada()
    {
        using var client = Client(SuperAdmin());
        var payload = new
        {
            versao = "2026.09.19-teste",
            endpoints = new object[]
            {
                new
                {
                    autorizador = "YY",
                    modelo = 55,
                    servico = 1,
                    ambiente = 1,
                    versaoServico = "4.00",
                    url = "https://homologacao.yy.gov.br/ws/status",
                    fonteUrl = "https://www.nfe.fazenda.gov.br/portal/webServices.aspx"
                },
                new
                {
                    autorizador = "YY",
                    modelo = 99, // inválido
                    servico = 1,
                    ambiente = 1,
                    versaoServico = "4.00",
                    url = "https://example.com/invalido",
                    fonteUrl = (string?)null
                }
            }
        };

        var first = await client.PostAsJsonAsync("/api/fiscal/sefaz-endpoints/importar", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var report1 = await first.Content.ReadFromJsonAsync<ImportReport>();
        Assert.NotNull(report1);
        Assert.Equal(2, report1!.Total);
        Assert.Equal(1, report1.Criados);
        Assert.Equal(1, report1.Rejeitados);
        Assert.Single(report1.Erros);

        var second = await client.PostAsJsonAsync("/api/fiscal/sefaz-endpoints/importar", payload);
        var report2 = await second.Content.ReadFromJsonAsync<ImportReport>();
        Assert.NotNull(report2);
        Assert.Equal(0, report2!.Criados);
        Assert.Equal(1, report2.Inalterados);
        Assert.Equal(1, report2.Rejeitados);
    }

    [Fact]
    public async Task Ambientes_SP_HomologacaoComEndpoints_ProducaoVazia()
    {
        using var client = Client(Tenant());
        var amb = await client.GetFromJsonAsync<AmbientesDto>("/api/fiscal/ufs/SP/ambientes");
        Assert.NotNull(amb);
        Assert.Equal("SP", amb!.Uf.Sigla);
        Assert.NotEmpty(amb.NFeHomologacao);
        Assert.All(amb.NFeHomologacao, e => Assert.StartsWith("https://", e.Url));
        // Produção de SP não foi confirmada na fonte oficial → lista vazia honesta (R3).
        Assert.Empty(amb.NFeProducao);
    }

    private sealed record UfDto(string Sigla, int CodigoIbge, string Nome);
    private sealed record EndpointDto(string Autorizador, string Url);
    private sealed record AmbientesDto(
        UfDto Uf,
        List<EndpointDto> NFeHomologacao,
        List<EndpointDto> NFeProducao,
        List<EndpointDto> NFCeHomologacao,
        List<EndpointDto> NFCeProducao);
    private sealed record ImportError(int Linha, string Erro);
    private sealed record ImportReport(
        int Total, int Criados, int Atualizados, int Inalterados, int Rejeitados,
        List<ImportError> Erros);
}
