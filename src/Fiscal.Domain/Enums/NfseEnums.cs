namespace Fiscal.Domain.Enums;

/// <summary>
/// Modo de emissão NFS-e do município. Adesão ≠ emissão ativa: "somente
/// integrado ao ADN" mantém emissor próprio e NÃO usa o nacional.
/// </summary>
public enum ModoEmissaoNfse
{
    Desconhecido = 0,
    NacionalEmissorPublico = 1,
    SomenteAdn = 2,
    ProvedorMunicipal = 3,
    NaoAderiu = 4
}
