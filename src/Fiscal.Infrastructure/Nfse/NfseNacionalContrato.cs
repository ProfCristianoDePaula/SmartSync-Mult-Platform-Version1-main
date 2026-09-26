namespace Fiscal.Infrastructure.Nfse;

/// <summary>
/// Contrato do Padrão Nacional (DPS → Sefin) como DADO versionado.
/// Estrutura conforme o padrão documentado (DPS assinada, GZip+Base64, mTLS);
/// caminhos, nós e tamanhos DEVEM ser confrontados com manual/OpenAPI/schemas
/// vigentes antes de homologação real (PENDENCIAS.md F0-28). Nada aqui afirma
/// cobertura municipal: município conveniado ≠ emissor disponível.
/// </summary>
public static class NfseNacionalContrato
{
    public const string VersaoDocumentacao = "manual do contribuinte v1.2 (out/2025) — reconfirmar";
    public const string PathEmissao = "/dpse";
    public const string PathConsulta = "/nfse";
    public const string PathEvento = "/eventos";
    public const string ContentType = "application/json";

    public static class Nos
    {
        public const string RaizDps = "dps";
        public const string RaizNfse = "nfse";
        public const string Chave = "chaveAcesso";
        public const string Numero = "numero";
        public const string CodigoVerificacao = "codigoVerificacao";
        public const string Motivos = "motivos";
    }
}
