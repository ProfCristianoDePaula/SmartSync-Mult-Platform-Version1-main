using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// NFS-e municipal (Fiscal-3): seed, importação idempotente com override
/// preservado, situação por IBGE e isolamento de escrita (R5).
/// </summary>
[Collection("integration")]
public sealed class FiscalNfseTests(FiscalApiFixture fixture)
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

    private string Tenant()
    {
        fixture.ModuleActive = true;
        return fixture.IssueToken(FiscalApiFixture.TenantA, "TenantAdmin");
    }

    private static MultipartFormDataContent CsvFile(string csv, string name = "municipios.csv")
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "arquivo", name);
        return content;
    }

    private const string CsvBase =
        "codigo_ibge;nome;uf;situacao;fonte\n" +
        "3106200;Belo Horizonte;MG;nacional;https://www.gov.br/nfse\n" +
        "5300108;Brasília;DF;adn;https://www.gov.br/nfse\n" +
        "3550308;São Paulo;SP;nacional;https://www.gov.br/nfse\n" +
        "3525300;Jaú;SP;desconhecido;https://www.gov.br/nfse\n" +
        "4106902;Curitiba;PR;municipal;https://www.gov.br/nfse\n" +
        "999;Inválida;XX;xx;\n";

    [Fact]
    public async Task Seed_DeveConterSaoPaulo_E_Jau()
    {
        using var client = Client(Tenant());
        var sp = await client.GetFromJsonAsync<SituacaoDto>("/api/fiscal/nfse/municipios/3550308/situacao");
        Assert.NotNull(sp);
        Assert.Equal("São Paulo", sp!.Municipio.Nome);
        var jau = await client.GetFromJsonAsync<SituacaoDto>("/api/fiscal/nfse/municipios/3525300/situacao");
        Assert.NotNull(jau);
        Assert.Equal("Jaú", jau!.Municipio.Nome);
    }

    [Fact]
    public async Task Importar_PreservaOverrideManual_E_RelataErros()
    {
        using var client = Client(SuperAdmin());

        var first = await client.PostAsync(
            "/api/fiscal/nfse/municipios/importar", CsvFile(CsvBase));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var r1 = await first.Content.ReadFromJsonAsync<ImportReport>();
        Assert.NotNull(r1);
        Assert.Equal(6, r1!.Total);
        Assert.Equal(1, r1.Rejeitados);
        Assert.Single(r1.Erros);

        // Override manual em BH.
        var over = await client.PutAsJsonAsync("/api/fiscal/nfse/municipios/3106200",
            new { modo = 3, observacao = "emissor próprio confirmado", motivo = "portaria municipal 123" });
        Assert.Equal(HttpStatusCode.OK, over.StatusCode);

        // Reimportação: override preservado (ignorado), resto inalterado.
        var second = await client.PostAsync(
            "/api/fiscal/nfse/municipios/importar", CsvFile(CsvBase));
        var r2 = await second.Content.ReadFromJsonAsync<ImportReport>();
        Assert.NotNull(r2);
        Assert.Equal(0, r2!.Criados);
        Assert.True(r2.IgnoradosManuais >= 1);

        using var tenant = Client(Tenant());
        var sit = await tenant.GetFromJsonAsync<SituacaoDto>("/api/fiscal/nfse/municipios/3106200/situacao");
        Assert.NotNull(sit);
        Assert.Equal(3, (int)sit!.Config.Modo); // municipal do override, não o importado
    }

    [Fact]
    public async Task Importar_ArquivoVazio_DeveRetornar400()
    {
        using var client = Client(SuperAdmin());
        var response = await client.PostAsync(
            "/api/fiscal/nfse/municipios/importar", CsvFile("codigo_ibge;nome;uf;situacao\n"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TenantAdmin_Importar_DeveRetornar403()
    {
        using var client = Client(Tenant());
        var response = await client.PostAsync(
            "/api/fiscal/nfse/municipios/importar", CsvFile(CsvBase));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Situacao_Desconhecido_TrazMensagemOrientativa()
    {
        using var client = Client(Tenant());
        var sit = await client.GetFromJsonAsync<SituacaoDto>("/api/fiscal/nfse/municipios/3525300/situacao");
        Assert.NotNull(sit);
        Assert.Contains("desconhecida", sit!.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MunicipioDto(string CodigoIbge, string Nome, string Uf);
    private sealed record ConfigDto(int Modo);
    private sealed record SituacaoDto(MunicipioDto Municipio, ConfigDto Config, string Mensagem);
    private sealed record ImportError(int Linha, string Erro);
    private sealed record ImportReport(
        int Total, int Criados, int Atualizados, int IgnoradosManuais, int Rejeitados,
        string ArquivoHash, List<ImportError> Erros);
}
