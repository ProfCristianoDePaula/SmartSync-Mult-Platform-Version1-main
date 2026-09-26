using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Fiscal.Application.Xml;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
namespace Fiscal.Tests;

/// <summary>
/// XML/XSD/assinatura (Fiscal-9, offline): golden XML no XSD didático,
/// assinatura verifica, 1 byte quebra, DTD/XXE rejeitados, sem NCM recusado,
/// pacote oficial ausente com mensagem clara. Nada é transmitido.
/// </summary>
[Collection("integration")]
public sealed class FiscalXmlTests(FiscalApiFixture fixture)
{
    private static NFeBuildInput EntradaMd55() => new(
        35, 55, "1", 123,
        2,
        new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Local),
        new NFeEmitData("11222333000181", "Loja Teste LTDA", "123456789", 3,
            "Rua Fiscal", "100", "Centro", "3550308", "São Paulo", "SP", "01310100"),
        new NFeDestData(null, "12345678909", "Cliente X",
            "Rua C", "10", "Centro", "3550308", "São Paulo", "SP", "01310100"),
        new[]
        {
            new NFeItemData(1, "P1", "Produto Um", "84713000", "5102", "UN", 2, 100m,
                "00", null, 18m, "000001", "00")
        },
        new NFeTotaisData(200m, 0m, 0m, 200m),
        "17",
        "Venda de mercadoria",
        null);

    private (INFeXmlBuilder Builder, IXsdValidator Validator, IXmlSigner Signer) Resolve()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return (
            scope.ServiceProvider.GetRequiredService<INFeXmlBuilder>(),
            scope.ServiceProvider.GetRequiredService<IXsdValidator>(),
            scope.ServiceProvider.GetRequiredService<IXmlSigner>());
    }

    [Fact]
    public void GoldenXml_PassaNoXsd_E_AssinaturaVerifica()
    {
        var (builder, validator, signer) = Resolve();
        var (xml, id) = builder.Construir(EntradaMd55());

        validator.Validar(xml, "simulador");

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=11222333000181", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = new X509Certificate2(
            req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1))
                .Export(X509ContentType.Pfx, "pw"), "pw");

        var assinado = signer.Assinar(xml, cert, id);
        Assert.True(signer.Verificar(assinado));
        validator.Validar(assinado, "simulador");
    }

    [Fact]
    public void Tamper_De1Byte_QuebraVerificacao()
    {
        var (builder, _, signer) = Resolve();
        var (xml, id) = builder.Construir(EntradaMd55());

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=11222333000181", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = new X509Certificate2(
            req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1))
                .Export(X509ContentType.Pfx, "pw"), "pw");

        var assinado = signer.Assinar(xml, cert, id);
        var adulterado = assinado.Replace("<vNF>200.00</vNF>", "<vNF>200.01</vNF>");
        Assert.NotEqual(assinado, adulterado);
        Assert.False(signer.Verificar(adulterado));
    }

    [Fact]
    public void Dtd_E_EntidadeExterna_Rejeitados()
    {
        var (_, validator, _) = Resolve();
        const string xxe = """<?xml version="1.0"?><!DOCTYPE r [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><nfeSimulada xmlns="urn:smartsync:fiscal:simulador"><aviso>&xxe;</aviso></nfeSimulada>""";
        Assert.ThrowsAny<Exception>(() => validator.Validar(xxe, "simulador"));
    }

    [Fact]
    public void Produto_SemNcm_RecusadoAntesDeGerar()
    {
        var (builder, _, _) = Resolve();
        var entrada = EntradaMd55() with
        {
            Itens = new[]
            {
                new NFeItemData(1, "P1", "Produto Um", "", "5102", "UN", 1, 10m,
                    "00", null, null, null, null)
            }
        };
        var ex = Assert.Throws<Fiscal.Domain.Common.BusinessRuleViolationException>(() => builder.Construir(entrada));
        Assert.Contains("NCM", ex.Message);
    }

    [Fact]
    public void PacoteOficial_Ausente_MensagemClara()
    {
        var (_, validator, _) = Resolve();
        var ex = Assert.Throws<Fiscal.Domain.Common.BusinessRuleViolationException>(
            () => validator.Validar("<nfeSimulada/>", "oficial/4.00"));
        Assert.Contains("não instalado", ex.Message);
    }

    [Fact]
    public void IbsCbs_Exigido_SoRegimeNormal()
    {
        Assert.True(Fiscal.Infrastructure.Xml.IbsCbsRegras.Exigido(3));
        Assert.False(Fiscal.Infrastructure.Xml.IbsCbsRegras.Exigido(1));
    }
}
