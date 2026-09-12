# Etapa 29 — Pedidos e Vendas — Etapa 3 (Regras de negócio: Cupons)

**Data:** 2026-09-12
**Status:** Feita — cadastro + AplicarCupomService + 3 prioridades + 8 testes

## Objetivo

Implementar regras de cadastro e aplicação de cupom com prioridade estrita e validações.

## Entregue

- `CupomService` CRUD (global / categoria / produto) com validação `IdCategoria` e `ProdutosIds` existentes, `Cupom.Create` (XOR, validade).
- `AplicarCupomService` com prioridade 1) `CupomProduto` 2) `IdCategoria` 3) Global; `ValorMinimoCompra` sobre subtotal elegível; `DataValidade`/`Quantidade`; cálculo `ValorDesconto` vs `PercDesconto`; decremento `Quantidade`.
- Repositórios `ICupomRepository`/`ICupomProdutoRepository`.
- `tests/PedidosVendasTests.cs` 8 testes (3 prioridades + XOR + validade + alias + parcelas + Venda cálculo).

## Decisões

- XOR desconto, ValorMinimo sobre elegível, PrecoMock 100 (evolução: `products.preco_venda`), IsCupomProduto espelha mas fonte é `CupomProduto` rows.

## Validação

- `dotnet test --filter CupomPrioridadeTests` 8/8 Passed, `dotnet build Estoque.slnx` 0 erros.
