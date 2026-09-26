namespace Fiscal.Domain.Enums;

public enum TipoItemFiscal
{
    Mercadoria = 1,
    Servico = 2
}

public enum TipoPessoaFiscal
{
    Fisica = 1,
    Juridica = 2
}

public enum IndicadorIe
{
    Contribuinte = 1,
    Isento = 2,
    NaoContribuinte = 9
}

public enum TipoOperacaoFiscal
{
    Venda = 1,
    Devolucao = 2,
    Remessa = 3,
    Retorno = 4,
    Outra = 9
}
