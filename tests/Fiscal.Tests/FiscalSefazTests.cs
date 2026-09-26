using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Fiscal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Transmissão em homologação (Fiscal-10): contratos SOAP com fixtures
/// (autorizado, rejeitado, fault, timeout), sem nenhuma chamada real.
/// </summary>
[Collection("integration")]
public sealed class FiscalSefazTests(FiscalApiFixture fixture)
{
    private const string RetAutorizado = """
        <retEnviNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00"><tpAmb>2</tpAmb><verAplic>TESTE</verAplic><cStat>100</cStat><xMotivo>Autorizado</xMotivo><cUF>35</cUF><protNFe versao="4.00"><infProt><tpAmb>2</tpAmb><verAplic>TESTE</verAplic><chNFe>KEY</chNFe><nProt>135260000000001</nProt><cStat>100</cStat><xMotivo>Autorizado</xMotivo></infProt></protNFe></retEnviNFe>
        """;

    private const string RetRejeitado = """
        <retEnviNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00"><tpAmb>2</tpAmb><verAplic>TESTE</verAplic><cStat>225</cStat><xMotivo>Rejeicao teste</xMotivo><cUF>35</cUF></retEnviNFe>
        """;

    private const string SoapFault = """
        <Fault xmlns="http://www.w3.org/2003/05/soap-envelope"><faultstring>Servidor ocupado</faultstring></Fault>
        """;

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

    private static byte[] GerarPfx(string cnpj, string senha)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={cnpj}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return cert.Export(X509ContentType.Pfx, senha);
    }

    private async Task<(Guid EmitenteId, Guid DocId)> PrepararDocumentoAsync(
        HttpClient client, string obs = "pedido normal", int tipo = 55)
    {
        var branch = Guid.NewGuid();
        var emit = await client.PostAsJsonAsync("/api/fiscal/emitentes", new
        {
            branchId = branch,
            cnpj = "11222333000181",
            razaoSocial = "Loja Sefaz LTDA",
            fantasia = "Loja Sefaz",
            crt = 3,
            endereco = new
            {
                street = "Rua S", number = "1", district = "Centro",
                city = "São Paulo", state = "SP", postalCode = "01310100",
                codigoIbgeMunicipio = "3550308"
            },
            telefone = "11999998888",
            email = "sefaz@loja.local"
        });
        Assert.Equal(HttpStatusCode.Created, emit.StatusCode);
        var emitente = await emit.Content.ReadFromJsonAsync<EmitenteDto>();
        Assert.NotNull(emitente);

        // Certificado + referência (modo homologação usa o cofre real de dev).
        using var up = new MultipartFormDataContent();
        up.Add(new ByteArrayContent(GerarPfx("11222333000181", "pw123")), "arquivo", "c.pfx");
        up.Add(new StringContent("pw123"), "senha");
        up.Add(new StringContent(branch.ToString()), "branchId");
        var upResp = await client.PostAsync("/api/fiscal/certificados", up);
        Assert.Equal(HttpStatusCode.Created, upResp.StatusCode);
        var cert = await upResp.Content.ReadFromJsonAsync<CertDto>();
        Assert.NotNull(cert);

        var cfg = await client.PutAsJsonAsync($"/api/fiscal/emitentes/{emitente!.Id}/configuracoes", new
        {
            tipo, ambiente = 1, habilitado = true, serie = "1",
            modoIntegracao = 1, referenciaCertificado = cert!.Id, cscId = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, cfg.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/fiscal/documentos")
        {
            Content = JsonContent.Create(new
            {
                emitenteId = emitente.Id,
                tipo,
                snapshotDestinatario = """{"nome":"Cliente X","cpf":"12345678909"}""",
                snapshotItens = """[{"codigo":"P1","descricao":"Produto Um","ncm":"84713000","cfop":"5102","unCom":"UN","qtd":1,"vu":100.00,"cClassTrib":"000001","cstIbsCbs":"00"}]""",
                snapshotTotais = $$"""{"bruto":100.00,"frete":0,"desconto":0,"obs":"{{obs}}"}""",
                observacaoPedido = obs
            })
        };
        request.Headers.Add("Idempotency-Key", $"SEFAZ-{Guid.NewGuid()}");
        var created = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var doc = await created.Content.ReadFromJsonAsync<DocumentoDto>();
        Assert.NotNull(doc);
        return (emitente.Id, doc!.Id);
    }

    [Fact]
    public async Task Transmitir_Homologacao_Autorizado_ComFixtures()
    {
        fixture.PularXsdOficial = true;
        try
        {
            using var client = Client(Tenant());
            var (emitenteId, docId) = await PrepararDocumentoAsync(client);

            fixture.SefazResponder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RetAutorizado, Encoding.UTF8, "application/soap+xml")
            };

            using var scope = fixture.Factory.Services.CreateScope();
            var sefaz = scope.ServiceProvider
                .GetRequiredService<Fiscal.Infrastructure.Sefaz.SefazAutorizador>();
            var doc = await scope.ServiceProvider
                .GetRequiredService<Fiscal.Infrastructure.Persistence.FiscalDbContext>()
                .DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == Fiscal.Domain.Common.DocumentoFiscalId.From(docId));
            Assert.NotNull(doc);
            var ctx = new Fiscal.Application.Emissao.DocumentoContexto(
                docId, Fiscal.Domain.Enums.TipoDocumentoFiscal.NFe55,
                Fiscal.Domain.Enums.AmbienteFiscal.Homologacao,
                "1", doc!.Numero, doc.ChaveAcesso, doc.SnapshotEmitente,
                doc.SnapshotDestinatario, doc.SnapshotItens, doc.SnapshotTotais);

            var resultado = await sefaz.TransmitirAsync(ctx, default);
            Assert.Equal(Fiscal.Application.Emissao.ResultadoTransmissao.Autorizado, resultado.Resultado);
            Assert.Equal("100", resultado.CStat);
            Assert.Equal("135260000000001", resultado.Protocolo);
        }
        finally
        {
            fixture.PularXsdOficial = false;
        }
    }

    [Fact]
    public async Task Transmitir_Rejeitado_E_Fault_E_Timeout()
    {
        fixture.PularXsdOficial = true;
        try
        {
            using var client = Client(Tenant());
            var (_, docId) = await PrepararDocumentoAsync(client);

            using var scope = fixture.Factory.Services.CreateScope();
            var sefaz = scope.ServiceProvider
                .GetRequiredService<Fiscal.Infrastructure.Sefaz.SefazAutorizador>();
            var db = scope.ServiceProvider
                .GetRequiredService<FiscalDbContext>();
            var doc = await db.DocumentosFiscais.FirstOrDefaultAsync(d => d.Id == Fiscal.Domain.Common.DocumentoFiscalId.From(docId));
            Assert.NotNull(doc);
            Fiscal.Application.Emissao.DocumentoContexto Ctx() => new(
                docId, Fiscal.Domain.Enums.TipoDocumentoFiscal.NFe55,
                Fiscal.Domain.Enums.AmbienteFiscal.Homologacao,
                "1", doc!.Numero, doc.ChaveAcesso, doc.SnapshotEmitente,
                doc.SnapshotDestinatario, doc.SnapshotItens, doc.SnapshotTotais);

            fixture.SefazResponder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RetRejeitado, Encoding.UTF8, "application/soap+xml")
            };
            var rej = await sefaz.TransmitirAsync(Ctx(), default);
            Assert.Equal(Fiscal.Application.Emissao.ResultadoTransmissao.Rejeitado, rej.Resultado);

            fixture.SefazResponder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SoapFault, Encoding.UTF8, "application/soap+xml")
            };
            var fault = await sefaz.TransmitirAsync(Ctx(), default);
            Assert.Equal(Fiscal.Application.Emissao.ResultadoTransmissao.Rejeitado, fault.Resultado);

            fixture.SefazResponder = _ => throw new TimeoutException("timeout");
            await Assert.ThrowsAsync<TimeoutException>(() => sefaz.TransmitirAsync(Ctx(), default));
        }
        finally
        {
            fixture.PularXsdOficial = false;
        }
    }

    [Fact]
    public async Task TestarConexao_Online_MarcaProntidao()
    {
        using var client = Client(Tenant());
        var (emitenteId, _) = await PrepararDocumentoAsync(client);

        fixture.SefazResponder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """<retConsStatServ xmlns="http://www.portalfiscal.inf.br/nfe" versao="4.00"><tpAmb>2</tpAmb><verAplic>TESTE</verAplic><cStat>107</cStat><xMotivo>Servico em operacao</xMotivo><cUF>35</cUF></retConsStatServ>""",
                Encoding.UTF8, "application/soap+xml")
        };

        var resp = await client.PostAsync($"/api/fiscal/emitentes/{emitenteId}/testar-conexao", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<TesteDto>();
        Assert.NotNull(body);
        Assert.True(body!.Online);
        Assert.Equal("107", body.CStat);

        var pront = await client.GetFromJsonAsync<ProntidaoDto>(
            $"/api/fiscal/emitentes/{emitenteId}/prontidao?tipo=55&ambiente=1");
        Assert.NotNull(pront);
        Assert.Contains(pront!.Itens, i => i.Item == "conexao-homologacao" && i.Ok);
    }

    [Fact]
    public void Envelopes_ContemNosEsperados_E_CStatTabela()
    {
        var lote = Fiscal.Infrastructure.Sefaz.SoapEnvelopes.LoteEnvio("000000000000001", "<infNFe>X</infNFe>");
        Assert.Contains("enviNFe", lote);
        Assert.Contains("indSinc", lote);

        var (cStat, motivo, prot, _) = Fiscal.Infrastructure.Sefaz.SefazSoapClient.Interpretar(RetAutorizado);
        Assert.Equal("100", cStat);
        Assert.Equal("135260000000001", prot);
        Assert.Equal(Fiscal.Application.Emissao.ResultadoTransmissao.Autorizado,
            Fiscal.Infrastructure.Sefaz.CStatTabela.EfeitoDe("100"));
        Assert.Equal(Fiscal.Application.Emissao.ResultadoTransmissao.Rejeitado,
            Fiscal.Infrastructure.Sefaz.CStatTabela.EfeitoDe("999"));
        Assert.Equal(Fiscal.Application.Emissao.ResultadoTransmissao.Aguardando,
            Fiscal.Infrastructure.Sefaz.CStatTabela.EfeitoDe("103"));
    }

    private sealed record EmitenteDto(Guid Id);
    private sealed record CertDto(Guid Id);
    private sealed record DocumentoDto(Guid Id);
    private sealed record TesteDto(bool Online, string? CStat);
    private sealed record ProntItem(string Item, bool Ok);
    private sealed record ProntidaoDto(List<ProntItem> Itens);
}

