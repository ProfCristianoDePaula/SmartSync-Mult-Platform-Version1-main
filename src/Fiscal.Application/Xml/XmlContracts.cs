namespace Fiscal.Application.Xml;

// ---------------------------------------------------------------------------
// Dados de entrada do builder (montados pelo assembler a partir dos cadastros)
// ---------------------------------------------------------------------------

public sealed record NFeEmitData(
    string Cnpj,
    string Nome,
    string? InscricaoEstadual,
    int Crt,
    string Logradouro,
    string Numero,
    string Bairro,
    string CodigoMunicipio,
    string NomeMunicipio,
    string Uf,
    string Cep);

public sealed record NFeDestData(
    string? Cnpj,
    string? Cpf,
    string Nome,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? CodigoMunicipio,
    string? NomeMunicipio,
    string? Uf,
    string? Cep);

public sealed record NFeItemData(
    int Numero,
    string Codigo,
    string Descricao,
    string Ncm,
    string Cfop,
    string UnidadeComercial,
    decimal Quantidade,
    decimal ValorUnitario,
    string? CstIcms,
    string? Csosn,
    decimal? AliquotaIcms,
    string? CClassTrib,
    string? CstIbsCbs);

public sealed record NFeTotaisData(
    decimal ValorProdutos,
    decimal ValorFrete,
    decimal ValorDesconto,
    decimal ValorNota);

public sealed record NFeBuildInput(
    int CUf,
    int Modelo,
    string Serie,
    int Numero,
    int TipoAmbiente,
    DateTime DataEmissao,
    NFeEmitData Emitente,
    NFeDestData Destinatario,
    IReadOnlyList<NFeItemData> Itens,
    NFeTotaisData Totais,
    string TPag,
    string? NaturezaOperacao,
    string? QrCode);

// ---------------------------------------------------------------------------
// Contratos
// ---------------------------------------------------------------------------

/// <summary>Constrói o XML da NF-e/NFC-e no leiaute da versão vigente.</summary>
public interface INFeXmlBuilder
{
    /// <summary>Devolve (xmlSemAssinatura, idParaAssinatura).</summary>
    (string Xml, string Id) Construir(NFeBuildInput input);
}

/// <summary>Valida XML contra schemas locais confiáveis (R8).</summary>
public interface IXsdValidator
{
    /// <param name="modo">"simulador" (XSD didático) ou "oficial/4.00" (pacote oficial).</param>
    void Validar(string xml, string modo);
}

/// <summary>Assinatura XML (enveloped + C14N, KeyInfo X509) e verificação.</summary>
public interface IXmlSigner
{
    string Assinar(string xml, System.Security.Cryptography.X509Certificates.X509Certificate2 certificado, string id);
    bool Verificar(string xmlAssinado);
}
