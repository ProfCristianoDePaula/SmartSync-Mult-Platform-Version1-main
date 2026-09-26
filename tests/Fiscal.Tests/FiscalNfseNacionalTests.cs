using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// NFS-e nacional (Fiscal-12): DPS, 422 fora do nacional, consulta por chave,
/// cancelamento e DANFSe — Sefin fake (nenhuma rede real).
/// </summary>
[Collection("integration")]
public sealed class FiscalNfseNacionalTests(FiscalApiFixture fixture)
{
    private const string NfseOk = """{"nfse":{"chaveAcesso":"3550308ABC123","numero":"000000001","codigoVerificacao":"XYZ"}}""";
    private const string NfseRejeitada = """{"motivos":[{"codigo":"001","mensagem":"DPS invalida"}]}""";
    private const string EventoOk = """{"sucesso":true,"protocolo":"EVT-1"}""";

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

    private string SuperAdmin() => fixture.IssueToken(null, "SuperAdmin");

    private async Task<(Guid EmitenteId, Guid ClienteId, Guid ProdutoId)> PrepararAsync(HttpClient client)
    {
        var branch = Guid.NewGuid();
        var emit = await client.PostAsJsonAsync("/api/fiscal/emitentes", new
        {
            branchId = branch,
            cnpj = "11222333000181",
            razaoSocial = "Prestadora LTDA",
            fantasia = "Prestadora",
            crt = 3,
            endereco = new
            {
                street = "Rua P", number = "1", district = "Centro",
                city = "São Paulo", state = "SP", postalCode = "01310100",
                codigoIbgeMunicipio = "3550308"
            },
            telefone = "11999998888",
            email = "nfse@prest.local"
        });
        Assert.Equal(HttpStatusCode.Created, emit.StatusCode);
        var emitente = await emit.Content.ReadFromJsonAsync<EmitenteDto>();

        var clienteId = Guid.NewGuid();
        var cli = await client.PutAsJsonAsync($"/api/fiscal/clientes/{clienteId}", new
        {
            tipoPessoa = 2,
            documento = "11444777000161",
            nome = "Tomadora SA",
            indicadorIe = 9,
            street = "Av T", number = "2", district = "Centro",
            city = "São Paulo", state = "SP", postalCode = "01310100",
            codigoIbgeMunicipio = "3550308",
            consumidorFinal = true
        });
        Assert.Equal(HttpStatusCode.OK, cli.StatusCode);

        var produtoId = Guid.NewGuid();
        var prod = await client.PutAsJsonAsync($"/api/fiscal/produtos/{produtoId}", new
        {
            tipo = 2,
            itemLc116 = "01.07",
            codTrib = "01",
            aliqIss = 2.0m
        });
        Assert.Equal(HttpStatusCode.OK, prod.StatusCode);

        // Município no modo nacional (override administrativo, SuperAdmin).
        using var super = Client(SuperAdmin());
        var over = await super.PutAsJsonAsync("/api/fiscal/nfse/municipios/3550308",
            new { modo = 1, observacao = "teste", motivo = "teste automatizado" });
        Assert.Equal(HttpStatusCode.OK, over.StatusCode);

        return (emitente!.Id, clienteId, produtoId);
    }

    private async Task<Guid> CriarDocNfseAsync(HttpClient client, Guid emitenteId, Guid clienteId, Guid produtoId, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonContent.Create(new
            {
                emitenteId,
                tipo = 200,
                snapshotDestinatario = $$"""{"clienteId":"{{clienteId}}","nome":"Tomadora SA"}""",
                snapshotItens = $$"""[{"produtoId":"{{produtoId}}","descricao":"Suporte tecnico"}]""",
                snapshotTotais = """{"bruto":500.00,"frete":0,"desconto":0}""",
                observacaoPedido = "servico normal"
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
    public async Task Dps_Monta_ComIBSCBS_QuandoPerfilTem()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var builder = scope.ServiceProvider
            .GetRequiredService<Fiscal.Infrastructure.Nfse.DpsBuilder>();
        var json = builder.Montar(
            Documento(
                """{"nome":"Tomadora SA","documento":"11444777000161"}""",
                """[{"descricao":"Suporte tecnico"}]"""),
            Emitente(),
            new Fiscal.Infrastructure.Nfse.DpsBuilder.TomadorDps(2, "11444777000161", "Tomadora SA", "3550308", "01310100"),
            new Fiscal.Infrastructure.Nfse.DpsBuilder.ServicoDps(true, "01.07", "01", 2m, "000001", "00"),
            1, "1");
        Assert.Contains("municipioEmissor", json);
        Assert.Contains("3550308", json);
        Assert.Contains("ibsCbs", json);
    }

    [Fact]
    public async Task Municipio_ForaDoNacional_422()
    {
        using var client = Client(Tenant());

        // Emitente sediado em Jaú.
        var branch = Guid.NewGuid();
        var emit = await client.PostAsJsonAsync("/api/fiscal/emitentes", new
        {
            branchId = branch,
            cnpj = "11222333000181",
            razaoSocial = "Prestadora Jau LTDA",
            fantasia = "Prestadora Jau",
            crt = 3,
            endereco = new
            {
                street = "Rua J", number = "1", district = "Centro",
                city = "Jaú", state = "SP", postalCode = "17206441",
                codigoIbgeMunicipio = "3525300"
            },
            telefone = "14999998888",
            email = "jau@prest.local"
        });
        Assert.Equal(HttpStatusCode.Created, emit.StatusCode);
        var emitenteJau = await emit.Content.ReadFromJsonAsync<EmitenteDto>();
        Assert.NotNull(emitenteJau);

        // Jaú em modo municipal (override, SuperAdmin).
        using var super = Client(SuperAdmin());
        var over = await super.PutAsJsonAsync("/api/fiscal/nfse/municipios/3525300",
            new { modo = 3, observacao = "emissor próprio", motivo = "teste automatizado" });
        Assert.Equal(HttpStatusCode.OK, over.StatusCode);

        var sit = await client.GetFromJsonAsync<SituacaoDto>("/api/fiscal/nfse/municipios/3525300/situacao");
        Assert.NotNull(sit);
        Assert.Equal(3, (int)sit!.Config.Modo);
        Assert.Contains("provedor municipal", sit.Mensagem);

        using var scope = fixture.Factory.Services.CreateScope();
        var provedor = scope.ServiceProvider
            .GetRequiredService<Fiscal.Application.Nfse.INfseProvider>();
        var ex = await Assert.ThrowsAsync<Fiscal.Domain.Common.BusinessRuleViolationException>(() =>
            provedor.EmitirAsync(new Fiscal.Application.Nfse.DpsRequest(
                Fiscal.Domain.Common.TenantId.From(FiscalApiFixture.TenantA),
                emitenteJau!.Id, Fiscal.Domain.Enums.AmbienteFiscal.Homologacao,
                Guid.NewGuid(), "3525300", "1", 1, "<dps/>", "01"), default));
        Assert.Equal("MunicipioSemEmissaoNacional", ex.Code);
    }

    [Fact]
    public async Task Pipeline_NFSe_Simulador_Autoriza_E_Danfse()
    {
        using var client = Client(Tenant());
        var (emitenteId, clienteId, produtoId) = await PrepararAsync(client);
        var docId = await CriarDocNfseAsync(client, emitenteId, clienteId, produtoId, $"NFSE-{Guid.NewGuid()}");
        await AguardarStatusAsync(client, docId, 7);

        var pdf = await client.GetByteArrayAsync($"/api/fiscal/documentos/{docId}/danfe");
        var texto = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF", texto);
        Assert.Contains("DANFSE", texto);
    }

    [Fact]
    public async Task Provedor_Emite_Consulta_Cancela_ComFixtures()
    {
        fixture.NfseResponder = (_, path) => path switch
        {
            "/dpse" => (true, NfseOk),
            "" => (true, NfseOk), // GET consulta (fake recebe só a URL)
            _ when path.StartsWith("/nfse") => (true, NfseOk),
            _ when path.StartsWith("/eventos") => (true, EventoOk),
            _ => (false, "{}")
        };

        using var scope = fixture.Factory.Services.CreateScope();
        var provedor = scope.ServiceProvider
            .GetRequiredService<Fiscal.Application.Nfse.INfseProvider>();

        await Assert.ThrowsAsync<Fiscal.Domain.Common.BusinessRuleViolationException>(() =>
            provedor.EmitirAsync(new Fiscal.Application.Nfse.DpsRequest(
                Fiscal.Domain.Common.TenantId.From(FiscalApiFixture.TenantA),
                Guid.NewGuid(), Fiscal.Domain.Enums.AmbienteFiscal.Homologacao,
                Guid.NewGuid(), "3550308", "1", 1, "<dps/>", "01"), default));

        var consulta = await provedor.ConsultarAsync("3550308ABC123", default);
        Assert.True(consulta.Sucesso);
        Assert.Equal("3550308ABC123", consulta.ChaveAcesso);

        var cancel = await provedor.CancelarAsync(
            new Fiscal.Application.Nfse.CancelarNfseRequest(Guid.NewGuid(), "3550308ABC123", "Erro de preenchimento."),
            default);
        Assert.True(cancel.Sucesso);
        Assert.Equal("EVT-1", cancel.Protocolo);
    }

    private static Fiscal.Domain.Entities.DocumentoFiscal Documento(
        string dest, string itens)
        => Fiscal.Domain.Entities.DocumentoFiscal.Criar(
            Fiscal.Domain.Common.TenantId.New(),
            Fiscal.Domain.Common.EmitenteFiscalId.New(),
            Fiscal.Domain.Enums.TipoDocumentoFiscal.NFSe,
            Fiscal.Domain.Enums.AmbienteFiscal.Homologacao,
            "1", 1, null, null, null, $"k-{Guid.NewGuid()}", "h",
            "{}", dest, itens, "{}");

    private static Fiscal.Domain.Entities.EmitenteFiscal Emitente()
        => Fiscal.Domain.Entities.EmitenteFiscal.Create(
            Fiscal.Domain.Common.TenantId.New(),
            Fiscal.Domain.Common.BranchId.New(),
            "11222333000181", "Prestadora LTDA", "Prestadora",
            null, "12345", null,
            Fiscal.Domain.Enums.CrtFiscal.RegimeNormal,
            new Fiscal.Domain.ValueObjects.FiscalAddress(
                "Rua P", "1", null, "Centro", "São Paulo", "SP", "01310100", "3550308"),
            "11999998888", "nfse@prest.local");

    private sealed record EmitenteDto(Guid Id);
    private sealed record DocumentoDto(Guid Id, int Status);
    private sealed record ConfigDto(int Modo);
    private sealed record SituacaoDto(ConfigDto Config, string Mensagem);
}
