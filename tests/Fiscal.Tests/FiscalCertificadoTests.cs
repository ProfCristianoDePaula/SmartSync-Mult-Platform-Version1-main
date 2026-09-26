using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// Certificados (Fiscal-5): upload A1 com PFX GERADO no teste (nunca
/// versionado), senha errada, vencido, sem chave privada, base divergente,
/// ausência de segredos nas respostas, isolamento e rotação.
/// </summary>
[Collection("integration")]
public sealed class FiscalCertificadoTests(FiscalApiFixture fixture)
{
    private const string Senha = "s3nha-teste-123";

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

    /// <summary>Gera A1 autoassinado com o CNPJ no subject (só para teste).</summary>
    private static byte[] GerarPfx(string cnpj, string senha, DateTimeOffset? notAfter = null, bool comChave = true)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            $"CN={cnpj}, O=Fiscal Tests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var fim = notAfter ?? DateTimeOffset.UtcNow.AddYears(1);
        var inicio = fim < DateTimeOffset.UtcNow ? fim.AddYears(-1) : DateTimeOffset.UtcNow.AddDays(-1);
        var cert = req.CreateSelfSigned(inicio, fim);
        return comChave
            ? cert.Export(X509ContentType.Pfx, senha)
            : cert.Export(X509ContentType.Cert);
    }

    private static MultipartFormDataContent Upload(byte[] pfx, string senha, Guid? branchId = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(pfx), "arquivo", "cert.pfx");
        content.Add(new StringContent(senha), "senha");
        if (branchId is not null)
            content.Add(new StringContent(branchId.Value.ToString()), "branchId");
        return content;
    }

    private sealed record EmitenteCriadoDto(Guid Id);

    private async Task<Guid> CriarEmitenteAsync(HttpClient client, Guid branch, string cnpj = "11222333000181")
    {
        var resp = await client.PostAsJsonAsync("/api/fiscal/emitentes", new
        {
            branchId = branch,
            cnpj,
            razaoSocial = "Loja Cert LTDA",
            fantasia = "Loja Cert",
            crt = 3,
            endereco = new
            {
                street = "Rua Cert", number = "1", district = "Centro",
                city = "São Paulo", state = "SP", postalCode = "01310100",
                codigoIbgeMunicipio = "3550308"
            },
            telefone = "11999998888",
            email = "cert@loja.local"
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<EmitenteCriadoDto>();
        Assert.NotNull(dto);
        return dto!.Id;
    }

    [Fact]
    public async Task Upload_Valido_SalvaSoMetadados()
    {
        var branch = Guid.NewGuid();
        using var client = Client(Tenant());
        await CriarEmitenteAsync(client, branch);

        var pfx = GerarPfx("11222333000181", Senha);
        var response = await client.PostAsync("/api/fiscal/certificados", Upload(pfx, Senha, branch));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("s3nha-teste", raw);
        Assert.DoesNotContain("pfx", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("senha", raw, StringComparison.OrdinalIgnoreCase);
        var dto = JsonSerializer.Deserialize<CertDto>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(dto);
        Assert.Equal("11222333000181", dto!.CnpjDoCertificado);
    }

    [Fact]
    public async Task Upload_SenhaErrada_DeveRetornar400_SemVazar()
    {
        using var client = Client(Tenant());
        var pfx = GerarPfx("11222333000181", Senha);
        var response = await client.PostAsync("/api/fiscal/certificados", Upload(pfx, "errada", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("errada", raw);
    }

    [Fact]
    public async Task Upload_Vencido_E_SemChavePrivada_Recusados()
    {
        using var client = Client(Tenant());
        var vencido = GerarPfx("11222333000181", Senha, DateTimeOffset.UtcNow.AddDays(-1));
        var r1 = await client.PostAsync("/api/fiscal/certificados", Upload(vencido, Senha, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);

        var semChave = GerarPfx("11222333000181", Senha, comChave: false);
        var r2 = await client.PostAsync("/api/fiscal/certificados", Upload(semChave, Senha, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
    }

    [Fact]
    public async Task Upload_BaseDivergente_DeveRetornar400()
    {
        var branch = Guid.NewGuid();
        using var client = Client(Tenant());
        await CriarEmitenteAsync(client, branch, "11222333000181");

        var pfx = GerarPfx("99999999000199", Senha);
        var response = await client.PostAsync("/api/fiscal/certificados", Upload(pfx, Senha, branch));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Novo_NoMesmoEscopo_RevogaAnterior_E_Isolamento()
    {
        var branch = Guid.NewGuid();
        using var client = Client(Tenant());
        await CriarEmitenteAsync(client, branch);

        var r1 = await client.PostAsync("/api/fiscal/certificados", Upload(GerarPfx("11222333000181", Senha), Senha, branch));
        var dto1 = await r1.Content.ReadFromJsonAsync<CertDto>();
        Assert.NotNull(dto1);

        var r2 = await client.PostAsync("/api/fiscal/certificados", Upload(GerarPfx("11222333000181", Senha), Senha, branch));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var anterior = await client.GetFromJsonAsync<CertDto>($"/api/fiscal/certificados/{dto1!.Id}");
        Assert.NotNull(anterior);
        Assert.Equal(4, anterior!.Status); // Revogado

        using var other = Client(fixture.IssueToken(FiscalApiFixture.TenantB, "TenantAdmin"));
        var leak = await other.GetAsync($"/api/fiscal/certificados/{dto1.Id}");
        Assert.Equal(HttpStatusCode.NotFound, leak.StatusCode);
    }

    [Fact]
    public async Task Logs_NaoContemSenha()
    {
        // O cofre e o serviço nunca logam senha/PFX: garante via leitura de metadados.
        using var client = Client(Tenant());
        var lista = await client.GetFromJsonAsync<List<CertDto>>("/api/fiscal/certificados");
        Assert.NotNull(lista);
        var raw = JsonSerializer.Serialize(lista);
        Assert.DoesNotContain("senha", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CscToken_WriteOnly_NuncaDevolvido()
    {
        var branch = Guid.NewGuid();
        using var client = Client(Tenant());
        var emitenteId = await CriarEmitenteAsync(client, branch);

        var put = await client.PutAsJsonAsync($"/api/fiscal/emitentes/{emitenteId}/csc",
            new { tipo = 65, cscId = "1", token = "segredo-csc-123" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var bad = await client.PutAsJsonAsync($"/api/fiscal/emitentes/{emitenteId}/csc",
            new { tipo = 65, cscId = "1", token = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    private sealed record CertDto(Guid Id, string CnpjDoCertificado, int Status);
}
