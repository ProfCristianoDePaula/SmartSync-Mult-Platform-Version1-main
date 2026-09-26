using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Cadastros fiscais (Fiscal-6): perfis, validação formal, CSV e isolamento.
/// </summary>
[Collection("integration")]
public sealed class FiscalCadastroTests(FiscalApiFixture fixture)
{
    private HttpClient Client(string? token)
    {
        var client = fixture.Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string Tenant(string role = "TenantAdmin")
    {
        fixture.ModuleActive = true;
        return fixture.IssueToken(FiscalApiFixture.TenantA, role);
    }

    private static object ProdutoMercadoria() => new
    {
        tipo = 1,
        ncm = "84713000",
        cest = (string?)null,
        origem = "0",
        unCom = "UN",
        unTrib = "UN",
        fator = (decimal?)null,
        gtin = "SEM GTIN",
        cfopDentro = "5102",
        cfopFora = "6102",
        cstIcms = (string?)null,
        csosn = "102",
        aliqIcms = (decimal?)null
    };

    [Fact]
    public async Task Produto_Completo_FicaPronto_NFe()
    {
        var produtoId = Guid.NewGuid();
        using var client = Client(Tenant());
        var upsert = await client.PutAsJsonAsync($"/api/fiscal/produtos/{produtoId}", ProdutoMercadoria());
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var dto = await upsert.Content.ReadFromJsonAsync<ProdutoDto>();
        Assert.NotNull(dto);
        Assert.True(dto!.ProntoNFe);
        Assert.Empty(dto.Pendencias);
    }

    [Fact]
    public async Task Produto_SemNcm_E_CrtNormalSemCst_GeraPendencias()
    {
        var produtoId = Guid.NewGuid();
        using var client = Client(Tenant());
        var payload = new Dictionary<string, object?>
        {
            ["tipo"] = 1, ["cfopDentro"] = "5102", ["cfopFora"] = "6102",
            ["unCom"] = "UN", ["csosn"] = "102"
        };
        var upsert = await client.PutAsJsonAsync($"/api/fiscal/produtos/{produtoId}", payload);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var pend = await client.GetFromJsonAsync<List<ProdutoDto>>(
            $"/api/fiscal/produtos/pendencias?produtoId={produtoId}&crt=3");
        Assert.NotNull(pend);
        var item = pend!.Single();
        Assert.False(item.ProntoNFe);
        Assert.Contains(item.Pendencias, p => p.Contains("NCM"));
        Assert.Contains(item.Pendencias, p => p.Contains("CST"));
    }

    [Fact]
    public async Task Produto_NcmInvalido_DeveRetornar400()
    {
        using var client = Client(Tenant());
        var payload = new Dictionary<string, object?> { ["tipo"] = 1, ["ncm"] = "123" };
        var response = await client.PutAsJsonAsync($"/api/fiscal/produtos/{Guid.NewGuid()}", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Csv_Importa_ComErroPorLinha()
    {
        var p1 = Guid.NewGuid();
        var csv = "produtoId;tipo;ncm;cest;origem;unCom;unTrib;fator;gtin;cfopDentro;cfopFora;cstIcms;csosn;aliqIcms;itemLc116;nbs;codTrib;aliqIss\n" +
                  $"{p1};1;84713000;;;UN;UN;;SEM GTIN;5102;6102;;102;;;;;;\n" +
                  $"invalido;1;123\n";
        using var client = Client(Tenant());
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "arquivo", "produtos.csv");
        var response = await client.PostAsync("/api/fiscal/produtos/importar", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<CsvReport>();
        Assert.NotNull(report);
        Assert.Equal(2, report!.Total);
        Assert.Equal(1, report.Criados);
        Assert.Equal(1, report.Rejeitados);
    }

    [Fact]
    public async Task Cliente_ContribuinteSemIe_GeraPendencia()
    {
        var clienteId = Guid.NewGuid();
        using var client = Client(Tenant());
        var upsert = await client.PutAsJsonAsync($"/api/fiscal/clientes/{clienteId}", new
        {
            tipoPessoa = 2,
            documento = "11222333000181",
            nome = "Empresa Cliente LTDA",
            indicadorIe = 1,
            inscricaoEstadual = (string?)null,
            street = "Rua C", number = "10", district = "Centro",
            city = "São Paulo", state = "SP", postalCode = "01310100",
            codigoIbgeMunicipio = "3550308",
            consumidorFinal = false
        });
        // IE ausente para contribuinte: 400 na gravação (validação formal).
        Assert.Equal(HttpStatusCode.BadRequest, upsert.StatusCode);

        var ok = await client.PutAsJsonAsync($"/api/fiscal/clientes/{clienteId}", new
        {
            tipoPessoa = 2,
            documento = "11222333000181",
            nome = "Empresa Cliente LTDA",
            indicadorIe = 1,
            inscricaoEstadual = "123456789",
            street = "Rua C", number = "10", district = "Centro",
            city = "São Paulo", state = "SP", postalCode = "01310100",
            codigoIbgeMunicipio = "3550308",
            consumidorFinal = false
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var validacao = await client.GetFromJsonAsync<List<string>>(
            $"/api/fiscal/clientes/{clienteId}/validacao");
        Assert.NotNull(validacao);
        Assert.Empty(validacao!);
    }

    [Fact]
    public async Task Natureza_CRUD_ComIsolamento()
    {
        using var client = Client(Tenant());
        var created = await client.PostAsJsonAsync("/api/fiscal/naturezas",
            new { codigo = "VENDA", descricao = "Venda de mercadoria", tipoOperacao = 1 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var dup = await client.PostAsJsonAsync("/api/fiscal/naturezas",
            new { codigo = "venda", descricao = "Duplicada", tipoOperacao = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);

        fixture.ModuleActive = true;
        using var other = Client(fixture.IssueToken(FiscalApiFixture.TenantB, "TenantAdmin"));
        var list = await other.GetFromJsonAsync<List<object>>("/api/fiscal/naturezas");
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    private sealed record ProdutoDto(bool ProntoNFe, List<string> Pendencias);
    private sealed record CsvError(int Linha, string Erro);
    private sealed record CsvReport(int Total, int Criados, int Atualizados, int Rejeitados, List<CsvError> Erros);
}
