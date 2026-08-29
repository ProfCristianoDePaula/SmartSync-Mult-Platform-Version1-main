# Etapa 25 — Finalização do Planejamento (Estoque)

**Data:** 22/08/2026
**Status:** Feita (final)

## Objetivo

Fechar o ciclo de **planejamento** do microsserviço de Estoque: consolidar o
que foi projetado (Etapa 23) vs. entregue (Etapa 24), publicar o contrato
oficial da API e registrar o estado final para a próxima fase (operação).

## O que foi entregue

### 1. Contrato oficial

- `docs/CONTRATO-ESTOQUE.md` — fonte da verdade da API do Estoque (13
  seções, nos moldes do `CONTRATO-IDENTIDADE.md`): 5 bounded contexts,
  13 agregados, 13 controllers REST, payloads, roles/policies (`tenant` +
  `module-estoque` fail-closed), estratégias de `tenant_id`/`branchId`,
  modelo de dados, códigos de erro e `curl`s por filial.

### 2. Matriz de rastreabilidade (14 contextos solicitados)

| Contexto solicitado | Status |
|---|---|
| 1. Produtos | ✅ `Product` (SKU/barcode únicos por tenant, sem cópia por filial) |
| 2. Marcas | ✅ `Brand` |
| 3. Modelos | ✅ `Model` (por marca) |
| 4. Categorias | ✅ `Category` (hierarquia opcional) |
| 5. Fornecedores | ✅ `Supplier` (documento único por tenant, imutável no PUT) |
| 6. Endereços | ✅ VO `Address` (de Supplier) |
| 7. Contatos | ✅ VO `Contact` (de Supplier) |
| 8. Movimentações | ✅ `StockMovement` + `StockBalance` (custo médio, `MovementApplier`) |
| 9. Regras de estoque | ✅ `StockRule` (upsert por produto[, filial]) |
| 10. Validade | ✅ `Lot` + varredura de vencimento |
| 11. Outlet | ✅ `OutletItem` (motivo + desconto) |
| 12. AutoCompra | ✅ `PurchaseSuggestion` (gerada por `ReplenishmentScanWorker`) |
| 13. Importação XML | ✅ `XmlImport` + `XmlImportWorker` (NF-e) |
| 14. Alertas | ✅ `Alert` (ruptura/excesso/validade/outlet, dedupe diário) |

Requisitos transversais: **JWT via Identity (JWKS)**, **multi-tenant** (todo
dado com `tenant_id`), **multi-filial** (saldos/movimentações/lotes/
regras/outlet por `branchId`), Clean Architecture + DDD + SOLID,
Postgres/Npgsql/EF Core, Docker (rede `identity-net`).

### 3. Planejamento encerrado

As seções 11–25 do `docs/memoria/INDEX.md` registram a rastreabilidade
completa. O ciclo de planejamento iniciado com a auditoria e o design (23)
e encerrado com a implementação + contrato (24–25) está **fechado**.

## Decisões de fechamento

- **Contrato como artefato de planejamento**: a API só existe se o contrato
  existe — `CONTRATO-ESTOQUE.md` é a entrega de planejamento, não um anexo.
- **Sem código nesta etapa**: correções de mapeamento EF (`ValueConverter`
  para props nullable, regra `ProductId == Sku.Create(...)` sem `.Value`,
  `MapInboundClaims=false`) já foram absorvidas na Etapa 24.
- **Próxima fase é operacional**: subir a stack integrada e validar o gate
  de módulo em ambiente compartilhado (ver pendências).

## Validação

- `docs/CONTRATO-ESTOQUE.md` criado e referenciado no `INDEX.md`.
- `Estoque.slnx` (5 projetos) compila **0 erros / 0 avisos**; suíte
  `estoque` em container **3/3**.

## Pendências / Próximos passos (pós-planejamento)

- Executar `docker compose up -d --build` com os dois bancos
  (`identity_postgres`, `estoque_postgres`) e validar JWKS real
  Identity↔Estoque + cadastro do módulo `estoque` pelo SuperAdmin.
- Ampliar a cobertura de testes para o CONTRATO-ESTOQUE (cada seção) e
  forçar os jobs em modo síncrono quando necessário.
- Avaliar a criação de `GET /api/tenants/me/branches` no Identity
  (pendência registrada nas Etapas 23–25) para endurecer a validação de
  `branchId` de roles operacionais.
