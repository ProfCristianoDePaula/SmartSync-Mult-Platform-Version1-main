namespace Fiscal.Domain.Enums;

/// <summary>Estados do documento fiscal (máquina de estados explícita).</summary>
public enum StatusDocumentoFiscal
{
    Pendente = 1,
    Validando = 2,
    Assinado = 3,
    EnvioPendente = 4,
    AguardandoProcessamento = 5,
    ResultadoDesconhecido = 6,
    Autorizado = 7,
    Rejeitado = 8,
    Cancelado = 9,
    ErroTecnico = 10
}

/// <summary>Tipo de evento vinculado ao documento.</summary>
public enum TipoEventoFiscal
{
    Cancelamento = 1,
    Correcao = 2,
    Inutilizacao = 3,
    Outro = 9
}
