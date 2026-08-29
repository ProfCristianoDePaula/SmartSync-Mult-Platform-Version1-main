namespace Estoque.Domain.Enums;

public enum AlertType
{
    Ruptura = 1,
    Excesso = 2,
    Validade = 3,
    Outlet = 4
}

public enum AlertSeverity
{
    Info = 1,
    Aviso = 2,
    Critico = 3
}

public enum PurchaseSuggestionStatus
{
    Aberta = 1,
    Aprovada = 2,
    Descartada = 3
}

public enum XmlImportStatus
{
    Recebida = 1,
    Processando = 2,
    Concluida = 3,
    Erro = 4
}

public enum OutletReason
{
    Avaria = 1,
    Devolucao = 2,
    VencimentoProximo = 3,
    Outro = 4
}
