using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Fiscal.Application.Xml;
using Fiscal.Domain.Common;

namespace Fiscal.Infrastructure.Xml;

/// <summary>
/// Assinatura XML (base didática do material §16): referência ao elemento com
/// o Id, enveloped + C14N, KeyInfo X509. Algoritmos fixados pelo contrato da
/// versão vigente (RSA-SHA256/SHA256 do leiaute 4.00 — reconfirmar, F0-23).
/// Assina o elemento infNFe correto, nunca o envelope; nunca altera depois.
/// </summary>
public sealed class XmlSigner : IXmlSigner
{
    public const string SignatureMethodRsaSha256 = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
    public const string DigestMethodSha256 = "http://www.w3.org/2001/04/xmlenc#sha256";

    public string Assinar(string xml, X509Certificate2 certificado, string id)
    {
        var documento = Carregar(xml);

        using var rsa = certificado.GetRSAPrivateKey()
            ?? throw new BusinessRuleViolationException("Certificado sem chave RSA.");

        var alvo = FindById(documento, id)
            ?? throw new BusinessRuleViolationException($"Elemento Id='{id}' não encontrado.");

        var signedXml = new SignedXml(alvo) { SigningKey = rsa };
        signedXml.SignedInfo!.SignatureMethod = SignatureMethodRsaSha256;
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;

        var referencia = new Reference { Uri = $"#{id}", DigestMethod = DigestMethodSha256 };
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        referencia.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(referencia);

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificado));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();

        // Como na NF-e real: Signature é irmã do infNFe (filha da raiz),
        // referenciando-o pelo Id — nunca dentro do elemento assinado.
        documento.DocumentElement!.AppendChild(
            documento.ImportNode(signedXml.GetXml(), true));
        return documento.OuterXml;
    }

    public bool Verificar(string xmlAssinado)
    {
        try
        {
            var documento = Carregar(xmlAssinado);
            var nsmgr = new XmlNamespaceManager(documento.NameTable);
            nsmgr.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
            var signatureNode = documento.SelectSingleNode("//ds:Signature", nsmgr);
            if (signatureNode is null) return false;

            var signedXml = new SignedXml(documento);
            signedXml.LoadXml((XmlElement)signatureNode);
            return signedXml.CheckSignature();
        }
        catch
        {
            return false;
        }
    }

    private static XmlDocument Carregar(string xml)
    {
        var documento = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var texto = new StringReader(xml);
        using var reader = XmlReader.Create(texto, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        documento.Load(reader);
        return documento;
    }

    private static XmlElement? FindById(XmlDocument documento, string id)
    {
        var nsmgr = new XmlNamespaceManager(documento.NameTable);
        nsmgr.AddNamespace("s", "urn:smartsync:fiscal:simulador");
        return documento.SelectSingleNode($"//s:infNFe[@Id='{id}']", nsmgr) as XmlElement
            ?? documento.SelectSingleNode($"//*[@Id='{id}']") as XmlElement;
    }
}
