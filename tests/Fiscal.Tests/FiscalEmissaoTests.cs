using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Pipeline de emissão (Fiscal-8): tabela do material de aula contra o
/// simulador — dados incompletos, duplicidade simultânea, conflito de chave,
/// timeout⇒desconhecido⇒consulta, 200-com-rejeição, conciliação pós-falha e
/// isolamento entre tenants. Postgres real.
/// </summary>
[Collection("integration")]
public sealed class FiscalEmissaoTests(FiscalApiFixture fixture)
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

    private async Task<Guid> CriarEmitenteAsync(HttpClient client)
    {
        var branch = Guid.NewGuid();
        var resp = await client.PostAsJsonAsync("/api/fiscal/emitentes", new
        {
            branchId = branch,
            cnpj = "11222333000181",
            razaoSocial = "Loja Emissao LTDA",
            fantasia = "Loja Emissao",
            crt = 3,
            endereco = new
            {
                street = "Rua E", number = "1", district = "Centro",
                city = "São Paulo", state = "SP", postalCode = "01310100",
                codigoIbgeMunicipio = "3550308"
            },
            telefone = "11999998888",
            email = "emissao@loja.local"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<EmitenteDto>();
        return dto!.Id;
    }

    private static object NovoDocumento(Guid emitenteId, string obs = "pedido normal") => new
    {
        emitenteId,
        tipo = 55,
        origemVendaId = (Guid?)null,
        origemPedidoId = (Guid?)null,
        snapshotDestinatario = """{"nome":"Cliente X","doc":"12345678909"}""",
        snapshotItens = """[{"cod":"P1","qtd":1,"vu":100.00}]""",
        snapshotTotais = $$"""{"bruto":100.00,"obs":"{{obs}}"}""",
        observacaoPedido = obs
    };

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
    public async Task Criar_DadosIncompletos_RecusaAntesDoEnvio()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);
        var response = await client.PostAsync("/api/fiscal/documentos",
            JsonBody(new { emitenteId, tipo = 55 }));
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest,
            $"esperado 400, veio {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Criar_SemIdempotencyKey_DeveRetornar400()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId))
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuasRequisicoesIguaisSimultaneas_UmDocumento()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);
        const string key = "PEDIDO-DUP-001";

        var tasks = Enumerable.Range(0, 2).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
            {
                Content = JsonBody(NovoDocumento(emitenteId))
            };
            request.Headers.Add("Idempotency-Key", key);
            return client.SendAsync(request);
        });
        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK,
            $"status inesperado {(int)r.StatusCode}"));

        var ids = new HashSet<Guid>();
        foreach (var r in responses)
        {
            var dto = await r.Content.ReadFromJsonAsync<DocumentoDto>();
            Assert.NotNull(dto);
            ids.Add(dto!.Id);
        }
        Assert.Single(ids);
    }

    [Fact]
    public async Task MesmaChave_ConteudoDiferente_Conflito409()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);
        const string key = "PEDIDO-CONFLITO-001";

        var r1 = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId, "primeiro"))
        };
        r1.Headers.Add("Idempotency-Key", key);
        var resp1 = await client.SendAsync(r1);
        Assert.Equal(HttpStatusCode.Accepted, resp1.StatusCode);

        var r2 = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId, "segundo-conteudo"))
        };
        r2.Headers.Add("Idempotency-Key", key);
        var resp2 = await client.SendAsync(r2);
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    [Fact]
    public async Task Timeout_AposEnvio_Desconhecido_DepoisConsultaAutoriza()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId, "pedido TIMEOUT teste"))
        };
        request.Headers.Add("Idempotency-Key", "PEDIDO-TIMEOUT-001");
        var created = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(dto);

        // Worker marca desconhecido (sem reenvio cego).
        await AguardarStatusAsync(client, dto!.Id, 6); // ResultadoDesconhecido

        // Conciliação manual encontra a autorização.
        var conc = await client.PostAsync($"/api/fiscal/documentos/{dto.Id}/consultar", null);
        Assert.Equal(HttpStatusCode.OK, conc.StatusCode);
        var final = await conc.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(final);
        Assert.Equal(7, (int)final!.Status); // Autorizado
        Assert.NotNull(final.Protocolo);
        Assert.Contains("SIMULACAO", final.Protocolo!);
    }

    [Fact]
    public async Task Http200ComRejeicao_NuncaViraAutorizacao()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId, "pedido REJEITAR teste"))
        };
        request.Headers.Add("Idempotency-Key", "PEDIDO-REJEITAR-001");
        var created = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(dto);

        await AguardarStatusAsync(client, dto!.Id, 8); // Rejeitado
        var final = await client.GetFromJsonAsync<DocumentoDto>($"/api/fiscal/documentos/{dto.Id}");
        Assert.NotNull(final);
        Assert.Equal(8, (int)final!.Status);
        Assert.Null(final.Protocolo);
    }

    [Fact]
    public async Task Cancelamento_FluxoCompleto_ComXml()
    {
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId))
        };
        request.Headers.Add("Idempotency-Key", "PEDIDO-CANCEL-001");
        var created = await client.SendAsync(request);
        var dto = await created.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(dto);
        await AguardarStatusAsync(client, dto!.Id, 7); // Autorizado

        var xml = await client.GetStringAsync($"/api/fiscal/documentos/{dto.Id}/xml");
        Assert.Contains("SIMULA", xml);

        var canc = await client.PostAsJsonAsync($"/api/fiscal/documentos/{dto.Id}/cancelamento",
            new { justificativa = "Cliente desistiu da compra teste." });
        Assert.Equal(HttpStatusCode.Accepted, canc.StatusCode);

        await AguardarStatusAsync(client, dto.Id, 9); // Cancelado
    }

    [Fact]
    public async Task TenantB_NaoAcessaDocumentoDoTenantA()
    {
        using var clientA = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(clientA);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonBody(NovoDocumento(emitenteId))
        };
        request.Headers.Add("Idempotency-Key", "PEDIDO-ISOL-001");
        var created = await clientA.SendAsync(request);
        var dto = await created.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(dto);

        fixture.ModuleActive = true;
        using var clientB = Client(fixture.IssueToken(FiscalApiFixture.TenantB, "TenantAdmin"));
        var get = await clientB.GetAsync($"/api/fiscal/documentos/{dto!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    private static System.Net.Http.Json.JsonContent JsonBody(object value)
        => JsonContent.Create(value);

    private sealed record EmitenteDto(Guid Id);
    private sealed record DocumentoDto(Guid Id, int Status, string? Protocolo);
}

