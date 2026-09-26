namespace Fiscal.Domain.Enums;

/// <summary>Regime tributário do emitente (sem colapsar Simples/MEI/especial).</summary>
public enum CrtFiscal
{
    SimplesNacional = 1,
    SimplesExcesso = 2,
    RegimeNormal = 3,
    Mei = 4
}

/// <summary>Tipo de documento da configuração.</summary>
public enum TipoDocumentoFiscal
{
    NFe55 = 55,
    NFCe65 = 65,
    NFSe = 200
}

/// <summary>Como o emitente integra cada tipo/ambiente.</summary>
public enum ModoIntegracao
{
    Simulador = 0,
    Homologacao = 1,
    Producao = 2
}

/// <summary>Escopo da concessão por unidade.</summary>
public enum PapelUnidade
{
    Configurar = 1,
    Emitir = 2
}
