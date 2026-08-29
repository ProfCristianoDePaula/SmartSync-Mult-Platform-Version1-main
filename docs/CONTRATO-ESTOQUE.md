# Contrato — Serviço de Estoque (Estoque & Catálogo)

> **Status:** Atualizado (Etapa 24) — revisar a cada nova etapa que altere a API.
> **Base:** OpenAPI 3.1 / Scalar (`/scalar/v1`) — fonte da verdade para payloads.
> **Autenticação:** JWT RS256 emitido pelo **Identity.Api** (`GET /api/auth/jwks`);
>   `MapInboundClaims=false` (claims literais: `user_id`, `tenant_id`, `role`).
> **Multi-tenant / multi-filial:** catálogo é do **tenant** (produto disponível
>   para todas as filiais, sem cópia por filial); saldos, movimentações, lotes,
>   regras e outlet são **por filial** ( `branchId` no payload/query).

---

## Índice

- [1. Visão geral](#1-visão-geral)
- [2. Autenticação e autorização](#2-autenticação-e-autorização)
- [3. Endpoints de Catálogo — Produtos](#3-endpoints-de-catálogo--produtos)
- [4. Endpoints de Catálogo — Marcas, Modelos e Categorias](#4-endpoints-de-catálogo--marcas-modelos-e-categorias)
- [5. Endpoints de Fornecedores](#5-endpoints-de-fornecedores)
- [6. Endpoints de Estoque — Movimentações e Saldos](#6-endpoints-de-estoque--movimentações-e-saldos)
- [7. Endpoints de Políticas — Regras, Lotes, Outlet e Alertas](#7-endpoints-de-políticas--regras-lotes-outlet-e-alertas)
- [8. Endpoints de AutoCompra](#8-endpoints-de-autocompra)
- [9. Endpoints de Integração — Importação XML](#9-endpoints-de-integração--importação-xml)
- [10. Modelo de dados](#10-modelo-de-dados)
- [11. Erros e códigos](#11-erros-e-códigos)
- [12. Exemplos (curl)](#12-exemplos-curl)
- [13. Referências](#13-referências)

---

## 1. Visão geral

O serviço de **Estoque** é o segundo microsserviço da plataforma. Ele
concentra os 5 bounded contexts planejados na Etapa 23:

- **Catálogo** — `Product` (SKU único por tenant), `Brand`, `Model`
  (sempre vinculado a uma marca), `Category` (hierarquia opcional) e
  `Supplier` (documento CPF/CNPJ único por tenant, com `Address`/`Contact`);
- **Movimentações & Saldos** — `StockBalance` (saldo por `tenant×produto×filial`,
  custo médio ponderado) e `StockMovement` (log append-only com tipos
  Entrada/Saída/Ajuste/Transferência);
- **Políticas** — `StockRule` (mínimo/ponto de pedido por produto[, filial]),
  `Lot` (validade por lote/filial), `OutletItem` (avaria/devolução/
  vencimento próximo) e `Alert` (ruptura/excesso/validade/outlet);
- **AutoCompra** — `PurchaseSuggestion` (gerada pelo motor de reposição);
- **Integração** — `XmlImport` (NF-e) + outbox (`outbox_messages`,
  padrão Transactional Outbox).

A integração com o **Identity** é **HTTP-only** (sem acesso direto ao banco
alheio, contrato §12.1): validação de JWT por JWKS
(`GET {IDENTITY_BASE_URL}/api/auth/jwks`), gate de módulo
(`GET /api/tenants/me/modules`, slug `estoque`, com cache) e validação de
filial (HTTP com cache; limitação v1 documentada — Etapa 23).

> Persistência: Postgres próprio `estoque` (database-per-service). Migrations
> aplicadas no arranque (`EstoqueDbInitializer`).

## 2. Autenticação e autorização

### 2.1 JWT

- O **Identity** emite access tokens RS256; o Estoque valida pela **chave
  pública** buscada em `Identity:BaseUrl` e cacheada em `JwksKeyStore`
  (refresh periódico por `JwksRefreshService`).
- Claims relevantes: `user_id` (executor da movimentação), `tenant_id`
  (obrigatório — fonte da verdade), `role`, `email_confirmed`. **`tenant_id`
  SEMPRE presente** (SuperAdmin global sem a claim recebe **403** — decisão da
  Etapa 23 para evitar impersonation).
- Header: `Authorization: Bearer <accessToken>`.

### 2.2 Policies

| Policy | Significado |
|--------|-------------|
| `tenant` | exige claim `tenant_id` (rotas de negócio do Estoque) |
| `module-estoque` | `tenant` + gate de módulo — consulta
  `GET /api/tenants/me/modules` com o token do usuário e verifica slug
  `estoque` ativo (cache 5 min, fail-CLOSED) |

### 2.3 Roles

| Role | Acesso no Estoque |
|------|-------------------|
| `TenantAdmin` | Tudo do tenant (catálogo, fornecedores, regras, importação) |
| `Manager` | Leitura geral + escrita (catálogo, movimentações, regras, importação); ajuste de inventário; transferências |
| `Seller` | Entrada/saída (operacional) + outlet |
| `Delivery` | Leitura geral |
| `Client` | Sem acesso direto ao Estoque (autorrelação não existe) |
| `SuperAdmin` | **403 sem claim** na v1 (sem operação cross-tenant) |

## 3. Endpoints de Catálogo — Produtos

Produto é **do tenant** — disponível para todas as filiais.

| Método | Rota | Roles de escrita | Descrição |
|--------|------|------------------|-----------|
| POST | `/api/products` | `TenantAdmin`, `Manager` | Cria produto |
| PUT | `/api/products/{id}` | `TenantAdmin`, `Manager` | Atualiza (nome/barcode/marca/modelo/categoria/uom) |
| DELETE | `/api/products/{id}` | `TenantAdmin`, `Manager` | Soft delete (libera SKU/barcode) |
| GET | `/api/products/{id}` | qualquer role do tenant | Detalhe |
| GET | `/api/products` | qualquer role do tenant | Lista paginada (`search`, `categoryId`, `brandId`, `includeInactive`, `page`, `pageSize`) |

`CreateProductCommand`: `sku` (≤64, único por tenant ativo, normalizado
upper), `name` (≤255), `barcode` (EAN-13 opcional único), `brandId?`,
`modelId?`, `categoryId?`, `unitOfMeasure` (enum). `TenantId` vem da claim.

## 4. Endpoints de Catálogo — Marcas, Modelos e Categorias

CRUDs idênticos (soft delete named filter `"Active"`). Unicidade por tenant
entre ativos.

| Método | Rota | Notas |
|--------|------|-------|
| POST | `/api/brands` | `name` único por tenant |
| PUT/DELETE/GET | `/api/brands/{id}`, `/api/brands` | — |
| POST | `/api/models` | `{ brandId, name }` — nome único por (tenant, marca) |
| GET | `/api/models` | `?brandId=` opcional |
| POST/PUT/DELETE/GET | `/api/categories` | `?parentCategoryId` opcional |

## 5. Endpoints de Fornecedores

Documento (CPF 11 / CNPJ 14) único **por tenant** entre ativos (índice parcial
composto; espelha a regra do Tenant do Identity). Documento é **imutável** no
PUT.

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/suppliers` | `name`, `tipoPessoa` (1/2), `documento`, `email`, `address`, `contact` |
| PUT | `/api/suppliers/{id}` | Edita apenas `name`/`email`/`address`/`contact` |
| DELETE | `/api/suppliers/{id}` | Soft delete |
| GET | `/api/suppliers` | Paginada (`search`, `page`, `pageSize`) |

VOs validados: `Cpf`/`Cnpj` (dígitos verificadores), `Address` (CEP 8,
UF 2) e `Contact` (telefone 10/11, e-mail de contato).

## 6. Endpoints de Estoque — Movimentações e Saldos

Movimentação é **append-only**; saldo é derivado (`StockBalance`) e atualizado
atomicamente com a movimentação na MESMA transação + outbox.

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/stock/in` | `TenantAdmin`, `Manager`, `Seller` | Entrada (+lote opcional por id ou novo `lotNumber`/`lotExpiresOn`) |
| POST | `/api/stock/out` | `TenantAdmin`, `Manager`, `Seller` | Saída — invariante **saldo ≥ 0** (400 se insuficiente; respeita `LotId` informado) |
| POST | `/api/stock/adjustments` | `TenantAdmin`, `Manager` | Ajuste com motivo obrigatório (auditoria) |
| POST | `/api/stock/transfers` | `TenantAdmin`, `Manager` | **Par espelhado atômico** (origem→destino, mesma quant) |
| GET | `/api/stock/movements` | qualquer role | Histórico (`branchId?`, `productId?`, `type?`, paginado) |
| GET | `/api/balances` | qualquer role | Saldos por `branchId` (opcional), `belowMinimumOnly`, paginado; retorno inclui `sku`/`productName` e custo médio |

`FilialId` chega no body/query e é validado contra o tenant do token
(ver §2, estratégia de FilialId da Etapa 23). `user_id` da claim é gravado
como `performedByUserId`.

## 7. Endpoints de Políticas — Regras, Lotes, Outlet e Alertas

### 7.1 Regras de reposição

| Método | Rota | Descrição |
|--------|------|-----------|
| PUT | `/api/stock-rules` | **Upsert** por (tenant, produto[, filial]); `minimumQuantity`/`reorderPoint` > 0 ; `reorderPoint ≥ minimumQuantity`; filial nula = default do tenant |
| GET | `/api/stock-rules` | Lista |

### 7.2 Lotes / Validade

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/lots/expiring` | Vencendo nos próximos N dias (`days`=1..365, `branchId?`, paginado) |

Lote: `number` único por (tenant, produto, filial) entre ativos.

### 7.3 Outlet

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/outlet-items` | Marca produto da filial como outlet (`reason`, `quantity`, `suggestedDiscountPct` 0–90) |
| PATCH | `/api/outlet-items/{id}/resolve` | Baixa o item (`TenantAdmin`, `Manager`) |
| GET | `/api/outlet-items` | Abertos por `branchId` (paginado) |

### 7.4 Alertas

Gerados pelos jobs (validade 6h, reposição horária).

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/alerts` | Sempre escopado por tenant (`unacknowledgedOnly`, `type`, paginado) |
| PATCH | `/api/alerts/{id}/acknowledge` | Reconhece (`Manager+`) |

## 8. Endpoints de AutoCompra

Sugestões geradas pelo motor de reposição (uma **aberta** por produto×filial).

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/api/purchase-suggestions` | Lista (`openOnly`, paginado) |
| PATCH | `/api/purchase-suggestions/{id}` | `approve: true/false` (Aprova/Descarta, `Manager+`) — guarda `suggestedQuantity`/`observedBalance` |

## 9. Endpoints de Integração — Importação XML

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/xml-imports` | Multipart `file` (≤5 MB) + `branchId` + `supplierId?`; 202 Accepted com `id` |
| GET | `/api/xml-imports/{id}` | Status (`Recebida→Processando→Concluida/Erro`, `skippedItems`, `errorMessage`) |

Parser NF-e minimalista: `<det>`→`cProd`=`sku`, `xProd`, `qCom`, `vUnCom`,
`uCom`; produtos ausentes são criados (converter `uCom`). Jobs processam a
fila.

## 10. Modelo de dados

Tabelas (Postgres `estoque`):

| Tabela | Notas |
|--------|-------|
| `products` | tenant_id, sku/barcode únicos por tenant entre ativos (índices parciais) |
| `brands` / `models` / `categories` | unicidade por tenant (modelo: por marca) |
| `suppliers` | (tenant_id, document_number) único entre ativos |
| `stock_balances` | (tenant_id, product_id, branch_id) único; CHECK quantity ≥ 0 |
| `stock_movements` | append-only |
| `stock_rules` | (tenant_id, product_id, branch_id NULLable) único entre ativos |
| `lots` | (tenant_id, branch_id, product_id, number) único entre ativos |
| `outlet_items` | marcação aberta |
| `purchase_suggestions` | uma aberta por (tenant, produto, filial) (`WHERE status=1`) |
| `xml_imports` | fila; content (bytea) |
| `alerts` | (tenant_id, type, product_id, branch_id, generatedOn) único p/ dedupe |
| `outbox_messages` | Transactional Outbox |

Todas as tabelas de negócio com `tenant_id`; soft delete via named filter.

## 11. Erros e códigos

| Código | Significado |
|--------|-------------|
| `400` | Payload inválido / regra de negócio violada (ex.: SKU duplicado; saldo insuficiente; ajuste sem motivo; regra com `reorderPoint < minimum`; lote vencido) |
| `401` | Sem token / token inválido (JWKS) |
| `403` | Sem claim `tenant_id` (SuperAdmin) ou role sem permissão ou módulo não contratado ou filial não pertence ao tenant |
| `404` | Recurso inexistente (produto/fornecedor/lote não encontrado no tenant) |
| `429` | `xml-import` rate limit excedido |

## 12. Exemplos (curl)

> Base: `http://localhost:8081` (compose `.env`). Identity em
> `http://localhost:8080`.

### 12.1 Criar marca e produto

```bash
curl -X POST http://localhost:8081/api/brands \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"ACME"}'

curl -X POST http://localhost:8081/api/products \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"sku":"SMART-001","name":"Caneta Smart","unitOfMeasure":1}'
```

### 12.2 Entrada e saldo

```bash
curl -X POST http://localhost:8081/api/stock/in \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"branchId\":\"$BRANCH\",\"productId\":\"$PRODUCT\",\"quantity\":10,\"unitCost\":5.50}"

curl "http://localhost:8081/api/balances?branchId=$BRANCH" -H "Authorization: Bearer $TOKEN"
```

### 12.3 Saída além do saldo → 400

```bash
curl -X POST http://localhost:8081/api/stock/out \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"branchId\":\"$BRANCH\",\"productId\":\"$PRODUCT\",\"quantity\":999}"
```

### 12.4 Transferência entre filiais

```bash
curl -X POST http://localhost:8081/api/stock/transfers \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"fromBranchId\":\"$A\",\"toBranchId\":\"$B\",\"productId\":\"$PRODUCT\",\"quantity\":5}"
```

### 12.5 Importação XML

```bash
curl -X POST http://localhost:8081/api/xml-imports \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@nfe.xml" -F "branchId=$BRANCH"
```

## 13. Referências

- [README.md](../README.md) — subir a stack e rodar os testes.
- [docs/memoria/](memoria/) — Etapas 00–24.
- Identity: `GET /openapi/v1.json` (Estoque expõe o próprio) & `/scalar/v1`.
