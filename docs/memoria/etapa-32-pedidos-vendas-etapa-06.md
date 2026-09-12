# Etapa 32 — Pedidos e Vendas — Etapa 6 (Seeds)

**Data:** 2026-09-12
**Status:** Feita — FormaPagto + PedidoStatus

## Objetivo

Semear catálogos de domínio.

## Entregue

- `EstoqueDbInitializer.SeedAsync` idempotente: 5 `FormaPagto` com GUIDs fixos (Pix 1, Transferência 1, Depósito 1, Cartão de Débito 1, Cartão de Crédito 12) via `FromValidated`.
- `PedidoStatus` enum sem tabela (persistido como `int`).

## Decisões

- Seeder dedicado (não `HasData`) seguindo `DbSeeder` pattern do Identity, GUIDs estáveis, `if (!AnyAsync())` idempotente.

## Validação

- `dotnet build` 0 erros, `ListAsync` em `/api/formas-pagto` retorna 5 registros após `MigrateAsync`.
