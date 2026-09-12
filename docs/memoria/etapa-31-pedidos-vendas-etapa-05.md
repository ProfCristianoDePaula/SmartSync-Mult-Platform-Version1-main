# Etapa 31 — Pedidos e Vendas — Etapa 5 (Conversão Pedido→Venda)

**Data:** 2026-09-12
**Status:** Feita — fechamento transacional + cópia itens + idempotência

## Objetivo

Transformar `Pedido` em `Venda` atomicamente (Etapas 4+5).

## Entregue

- `VendaService.FinalizarCompraAsync` em `ITransactionScopeFactory`: 1 tx cria `Venda` (`ValorBruto/Desconto/Líquido/Frete/Final`+`IsPago`+`QuantidadeParcelar`), copia `ProdutosPedido→ProdutosVenda`, `Pedido.Fechar(VendaEfetuada)`, decrementa cupom, `SaveChanges`+`Commit`.
- Idempotência: `GetByPedidoAsync` pré-insert + `UX_vendas_id_pedido` constraint.
- Repositórios `IVendaRepository`/`IProdutosVendaRepository`.

## Decisões

- Transação única (pedido+venda+cupom), `IsPago=false` (AguardandoPagamento), `NrPedido=null`, não baixa estoque automaticamente (sugestão `IReservaEstoqueService`+outbox como evolução futura).

## Validação

- `dotnet test` 11/11, transação testada via `StockFlowTests` (migrations intactas).
