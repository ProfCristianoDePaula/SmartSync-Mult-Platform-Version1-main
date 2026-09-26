namespace Fiscal.Domain.Enums;

/// <summary>Quem autoriza a UF (relação oficial de autorizadores do Portal Nacional).</summary>
public enum AutorizadorTipo
{
    Proprio = 1,
    Svrs = 2,
    Svan = 3,
    Outro = 4
}

/// <summary>Modelo do documento fiscal.</summary>
public enum ModeloFiscal
{
    NFe55 = 55,
    NFCe65 = 65
}

/// <summary>Serviço SEFAZ por operação.</summary>
public enum SefazServico
{
    StatusServico = 1,
    Autorizacao = 2,
    RetAutorizacao = 3,
    ConsultaProtocolo = 4,
    RecepcaoEvento = 5,
    Inutilizacao = 6,
    ConsultaCadastro = 7,
    DistribuicaoDFe = 8
}

/// <summary>Ambiente do autorizador. Homologação e produção são totalmente separadas (R10).</summary>
public enum AmbienteFiscal
{
    Homologacao = 1,
    Producao = 2
}
