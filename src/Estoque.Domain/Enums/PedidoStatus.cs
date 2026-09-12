namespace Estoque.Domain.Enums;

/// <summary>
/// Status do Pedido/Carrinho — tabela de domínio / enum persistido como int.
/// Valores estáveis (nunca reutilizar numeração).
/// </summary>
public enum PedidoStatus
{
    Aberto = 1,
    AguardandoPagamento = 2,
    PagamentoAprovado = 3,
    VendaEfetuada = 4,
    Cancelado = 5,

    /// <summary>Alias semântico para VendaEfetuada (fechado).</summary>
    Fechado = VendaEfetuada
}
