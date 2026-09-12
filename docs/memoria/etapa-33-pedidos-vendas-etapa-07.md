# Etapa 33 — Pedidos e Vendas — Etapa 7 (API/Endpoints)

**Data:** 2026-09-12
**Status:** Feita — 5 controllers 17 endpoints

## Objetivo

Expor REST do módulo com DTOs, validações, multi-tenancy, Swagger.

## Entregue

- `CuponsController` `/api/cupons` CRUD (TenantAdmin/Manager escrita).
- `CarrinhoController` `/api/carrinho` (consultar, add/remove itens, aplicar cupom, calcular frete, checkout→Venda).
- `PedidosController` `/api/pedidos` (listar/consultar/cancelar).
- `VendasController` `/api/vendas` (listar/consultar).
- `FormasPagtoController` `/api/formas-pagto`.
- DTOs nunca expõem entidades, `FluentValidation→ProblemDetails`, `GetRequiredTenantId`/`GetUserId`, OpenAPI/Scalar.

## Validação

- `dotnet build Estoque.Api` 0 erros, `dotnet test` 11/11, `MapOpenApi` gera spec com 17 operações.
