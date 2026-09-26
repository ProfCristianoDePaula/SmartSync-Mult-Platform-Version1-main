using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Arquivos (Fiscal-11): XML recuperável idêntico (hash), isolamento entre
/// tenants, PDF com marcação de homologação. Postgres + disco reais.
/// </summary>
[Collection("integration")]
public sealed class FiscalArquivosTests(FiscalApiFixture fixture)
{
    private HttpClient Client(string? token)
    {
        var client = fixture.Factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private string Tenant()
    {
        fixture.ModuleActive = true;
        return fixture.IssueToken(FiscalApiFixture.TenantA, "TenantAdmin");
    }

    private async Task<Guid> CriarEmitenteAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/fiscal/emitentes", new
        {
            branchId = Guid.NewGuid(),
            cnpj = "11222333000181",
            razaoSocial = "Loja Arq LTDA",
            fantasia = "Loja Arq",
            crt = 3,
            endereco = new
            {
                street = "Rua A", number = "1", district = "Centro",
                city = "São Paulo", state = "SP", postalCode = "01310100",
                codigoIbgeMunicipio = "3550308"
            },
            telefone = "11999998888",
            email = "arq@loja.local"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<EmitenteDto>();
        return dto!.Id;
    }

    private static async Task<Guid> CriarDocumentoAsync(HttpClient client, Guid emitenteId, string key, string obs = "pedido normal")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonContent.Create(new
            {
                emitenteId,
                tipo = 55,
                snapshotDestinatario = """{"nome":"Cliente X","cpf":"12345678909"}""",
                snapshotItens = """[{"codigo":"P1","descricao":"Produto Um","ncm":"84713000","cfop":"5102","unCom":"UN","qtd":1,"vu":10.00}]""",
                snapshotTotais = """{"bruto":10.00,"frete":0,"desconto":0}""",
                observacaoPedido = obs
            })
        };
        request.Headers.Add("Idempotency-Key", key);
        var created = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(dto);
        return dto!.Id;
    }

    private static async Task AguardarStatusAsync(HttpClient client, Guid id, int status, int tentativas = 40)
    {
        for (var i = 0; i < tentativas; i++)
        {
            var doc = await client.GetFromJsonAsync<DocumentoDto>($"/api/fiscal/documentos/{id}");
            if (doc is not null && (int)doc.Status == status)
                return;
            await Task.Delay(500);
        }
        throw new Xunit.Sdk.XunitException($"Documento {id} não chegou ao status {status}.");
    }

    [Fact]
    public async Task Xml_Arquivado_Recuperavel_Identico()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);
        var docId = await CriarDocumentoAsync(client, emitenteId, $"ARQ-{Guid.NewGuid()}");
        await AguardarStatusAsync(client, docId, 7); // Autorizado (simulador)

        var lista = await client.GetFromJsonAsync<List<string>>($"/api/fiscal/documentos/{docId}/arquivos");
        Assert.NotNull(lista);
        Assert.Contains("enviado.xml", lista!);
        Assert.Contains("resposta.json", lista);

        var xmlColuna = await client.GetStringAsync($"/api/fiscal/documentos/{docId}/xml");
        var baixado = await client.GetByteArrayAsync($"/api/fiscal/documentos/{docId}/arquivos/enviado.xml");
        Assert.Equal(xmlColuna, Encoding.UTF8.GetString(baixado));
    }

    [Fact]
    public async Task TenantB_NaoBaixaArquivoDoTenantA()
    {
        using var clientA = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(clientA);
        var docId = await CriarDocumentoAsync(clientA, emitenteId, $"ARQB-{Guid.NewGuid()}");
        await AguardarStatusAsync(clientA, docId, 7);

        fixture.ModuleActive = true;
        using var clientB = Client(fixture.IssueToken(FiscalApiFixture.TenantB, "TenantAdmin"));
        var resp = await clientB.GetAsync($"/api/fiscal/documentos/{docId}/arquivos/enviado.xml");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var travessia = await clientA.GetAsync($"/api/fiscal/documentos/{docId}/arquivos/..%2Fsegredo");
        Assert.True(travessia.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"travessia => {(int)travessia.StatusCode}");
    }

    [Fact]
    public async Task Danfe_Pdf_ComMarcacaoDeHomologacao()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);
        var docId = await CriarDocumentoAsync(client, emitenteId, $"DANFE-{Guid.NewGuid()}");
        await AguardarStatusAsync(client, docId, 7);

        var pdf = await client.GetByteArrayAsync($"/api/fiscal/documentos/{docId}/danfe");
        Assert.True(pdf.Length > 500, $"PDF pequeno demais: {pdf.Length}");
        var texto = Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF", texto);
        Assert.Contains("SEM VALOR FISCAL", texto);
        Assert.Contains("DANFE", texto);
    }

    private sealed record EmitenteDto(Guid Id);
    private sealed record DocumentoDto(Guid Id, int Status);
}
