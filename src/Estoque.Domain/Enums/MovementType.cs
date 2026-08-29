namespace Estoque.Domain.Enums;

/// <summary>
/// Tipo de movimentação de estoque (log append-only).
/// Transferência gera um PAR: TransferOut na origem + TransferIn no destino,
/// sempre na mesma transação.
/// </summary>
public enum MovementType
{
    Entrada = 1,
    Saida = 2,
    Ajuste = 3,
    TransferenciaSaida = 4,
    TransferenciaEntrada = 5
}
