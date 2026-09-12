# Etapa 30 — Pedidos e Vendas — Etapa 4 (Carrinho/Pedido e Checkout)

**Data:** 2026-09-12
**Status:** Feita — carrinho como Pedido Aberto + frete mock + parcelas

## Objetivo

Fluxo de carrinho (IdCliente/IdUnidade da claim) e checkout (CEP frete + forma pagto + parcelas).

## Entregue

- `PedidoService` `GetOrCreateCarrinhoAsync` (`TenantId`+`clienteId`+`unidade`), `AdicionarItemAsync`/`RemoverItemAsync` (upsert + `RecalcularValorTotalAsync`), `AplicarCupomAsync`, `ConsultarCarrinhoAsync`.
- `ICalculoFreteService` mock (15/20/25 por CEP + 0.5×qtd), `ITenantParcelamentoProvider` (null→fallback), `ICheckoutService` (`CalcularFreteAsync`, `ListarFormasPagtoAsync`, `FinalizarAsync→VendaService`).
- Regra parcelas: Pix/Transfer/Depósito/Débito=1, Crédito ≤ limite (`ITenantParcelamentoProvider??FormaPagto.Qtd`), validada no backend.

## Decisões

- Captura automática `GetUserId`/`GetRequiredTenantId`, `IdUnidade` do body (sem `GET /me/branches`), `ValorTotal` recalculado a cada alteração, frete injetável, limite tenant via provider (hoje null).

## Validação

- `dotnet build` 0 erros, integrado com `StockFlowTests` sem regressão.
