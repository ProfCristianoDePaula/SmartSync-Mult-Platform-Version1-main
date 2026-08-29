# Etapa 13 — CRUD de Tenants (com referência ao Plan)

> ⚠️ **Revisão (Etapa 15):** o modelo mudou. O `Tenant` **não tem mais `plan_id`**:
> `Tenant.PlanId`, o `CreateTenantCommand/UpdateTenantCommand/TenantDto` com plano,
> a FK e o `IX_tenants_plan_id` foram removidos. A contratação agora é feita pelo
> vínculo `tenant_modules` (TenantModule) via `/api/tenants/{tenantId}/modules`
> (SuperAdmin-only). Ver `etapa-15-modulos-e-planos-por-modulo.md`.

## Objetivo

Expor o CRUD completo de Tenants via API, agora que Plans (Etapa 12) existem
para serem referenciados. Inclui: FK `plan_id` no agregado Tenant, soft delete
com named query filter, filtros de listagem, e o reforço na Etapa 11 de que um
tenant **soft-deletado não pode mais autenticar** seus usuários.

## Decisão do usuário (registrada, confirmada antes de gerar os artefatos)

- **Plano OPCIONAL no nascimento do Tenant** (`plan_id` **nullable**): o tenant
  pode ser criado sem plano e tê-lo atribuído depois via `PUT /api/tenants/{id}`.
  Quando informado, o plano deve existir e estar **ativo** (`400` caso contrário).
- **Soft delete no Tenant** (mesma abordagem das etapas 02/12): `IsActive` +
  `DeletedAtUtc` + **named query filter** `HasQueryFilter("Active", t => t.IsActive)`.
  O enum `TenantStatus` (Active/Inactive/Suspended) permanece para o ciclo de
  vida operacional — conceitos distintos de soft delete.
- **Índices únicos de CNPJ/e-mail passam a ser PARCIAIS** (`WHERE is_active`):
  um tenant soft-deletado **libera** CNPJ/e-mail na camada do banco (a
  Application já os ignorava via query filter). Descoberta em teste: sem o
  índice parcial, a reutilização quebrava com `DbUpdateException` (500).

## O que foi feito

### 1. Domain

- `Tenant`:
  - nova propriedade `PlanId?` (referência ao plano de assinatura);
  - `IsActive` + `DeletedAtUtc` (soft delete), `IsActive=true` no construtor;
  - `Create(..., PlanId? planId = null)` (plano opcional);
  - `SetPlan(PlanId?)`, `Update(legalName, tradeName, email, planId, status)` e
    `SoftDelete()` (seta `IsActive=false` + `DeletedAtUtc`).
  - CNPJ permanece **imutável** (identidade do tenant); e-mail editável.

### 2. EF Core

- `TenantConfiguration`:
  - `plan_id` nullable (`uuid`), conversão `PlanId? ↔ Guid?`, FK
    `FK_tenants_plans_plan_id` → `plans.id` **`ON DELETE RESTRICT`**;
  - `is_active` (`boolean`, default `true` para backfill) e `deleted_at_utc`;
  - **índices parciais** `IX_tenants_cnpj` / `IX_tenants_email`
    (`IsUnique() + HasFilter("\"is_active\"")`);
  - `IX_tenants_plan_id`;
  - named query filter `HasQueryFilter("Active", t => t.IsActive)`.
- Migration `20260809214752_AddTenantPlanAndSoftDelete`:
  - drop dos índices globais → add `plan_id`/`is_active`/`deleted_at_utc` →
    recria índices **parciais** + índice/FK do plano.
  - ⚠️ ajustado manualmente: `is_active` com `defaultValue: true` (o EF gera
    `false`; com `false`, tenants existentes nasceriam "inativos" no backfill).

### 3. Application — `Tenants/`

- Commands/queries como DTOs (sem MediatR, consistente com Plans):
  `CreateTenantCommand`, `UpdateTenantCommand`, `SoftDeleteTenantCommand`,
  `ListTenantsQuery` (Page/PageSize/Status/Search/Cnpj/IncludeInactive),
  `GetTenantByIdQuery`; `TenantDto`.
- Validators FluentValidation: razão social/nome fantasia obrigatórios ≤ 255,
  e-mail no formato do VO do Domain, CNPJ com 14 dígitos (com/sem máscara),
  `Status` válido no update.
- `ITenantService` (Create/Update/SoftDelete/List/GetById).
- **Refactor**: `PagedResult<T>` movido de `Identity.Application.Plans` para
  `Identity.Application.Common` (compartilhado por Plans e Tenants; usings
  atualizados em PlanService, PlansController e PlansTests).

### 4. Infrastructure

- `TenantService`:
  - valida com FluentValidation; converte `ArgumentException` dos VOs
    (CNPJ/e-mail) em `BusinessRuleViolationException` (`400` — nunca 500);
  - **plano ativo quando informado**: consulta em `Plans` (o query filter já
    oculta planos inativos) → `BusinessRuleViolationException`
    "tenant.plan.invalid";
  - reutiliza `TenantUniquenessValidator` (Etapa 04) — CNPJ/e-mail GLOBAIS,
    com `excludeTenantId` no update;
  - soft delete via query filter; listagem com filtros: `Status` (enum),
    `Search` (`ILIKE` em legal_name/trade_name), `Cnpj` (dígitos exatos),
    `IncludeInactive` → `IgnoreQueryFilters(["Active"])`; paginação (clamp 1–100).
- DI: `AddScoped<ITenantService, TenantService>()`.

### 5. API — `TenantsController`

- `[Authorize(Roles = Roles.SuperAdmin)]` em todos os endpoints (gestão de
  tenants é nível plataforma — mesmo padrão do PlansController).
- `POST /api/tenants` → `201 Created` (Location GET /api/tenants/{id}).
- `PUT /api/tenants/{id}` → `200`; `404` se inexistente/inativo.
- `DELETE /api/tenants/{id}` → `204` (soft delete); `404`.
- `GET /api/tenants?page&pageSize&status&search&cnpj&includeInactive` → `200`.
- `GET /api/tenants/{id}` → `200`; `404` (inativos ocultados pelo query filter).
- FluentValidation → `400` ProblemDetails "Dados inválidos.";
  `BusinessRuleViolationException` → `400` "Regra de negócio violada.".

### 6. Reforço na autenticação (Etapa 11/Empresa)

- `AuthService.LoginAsync` e `RefreshAsync`: usuário com `TenantId` só
  autentica se o tenant existir e estiver **ativo** (`IsActive=true`). O query
  filter oculta tenants soft-deletados → `IsTenantUsableForAuthAsync` retorna
  `false` → `401`. Usuários globais (TenantId nulo) não passam pela checagem.
- `RegisterAsync` já ganha o comportamento automaticamente: a checagem de
  existência do tenant usa o query filter, então registrar Client em tenant
  soft-deletado devolve "Tenant não encontrado."

### 7. Testes (64/64 passando)

- `tests/Identity.Tests/TenantsTests.cs` (19 testes novos): 201 sem plano /
  com plano ativo; 400 plano inexistente / plano inativo; 401 sem token; 403
  Client; 400 CNPJ obrigatório / CNPJ duplicado / e-mail duplicado; paginação
  (relativa); filtro por CNPJ (total exato = 1) e por status; GET 200/404;
  PUT 200 (troca de nome + atribuição de plano, CNPJ imutável) / 404; DELETE
  204 + oculto da lista + `includeInactive` (IsActive=false/DeletedAtUtc) +
  GET 404 + **reuso de CNPJ**; DELETE inexistente 404;
  **`TenantSoftDeletado_BloqueiaAutenticacaoDosUsuarios`** (login 200 → soft
  delete → login 401).
- Helper novo: `TestData.CreatePlanAsync` (planos direto no banco de teste).
- Suíte anterior continua verde (45 anteriores + 19 novos).

### 8. Docker / OpenAPI / Scalar

- Rebuild + `docker compose up -d --no-deps api`; healthcheck OK. Migration
  `20260809214752_AddTenantPlanAndSoftDelete` aplicada automaticamente no boot
  (colunas `plan_id`/`is_active`/`deleted_at_utc` + índices parciais `WHERE
  is_active` + FK `FK_tenants_plans_plan_id` confirmados via `pg_indexes`).
- OpenAPI `/openapi/v1.json`: `/api/tenants` (POST/GET) e `/api/tenants/{id}`
  (PUT/DELETE/GET), com `security: Bearer`; `/scalar/v1` responde 200.
- Smoke no container (SuperAdmin de seed): criar sem plano (`planId=null`) e com
  plano; listar/filtros (cnpj→1, search→1); detalhe; update (nome + plano);
  delete 204 → oculto da lista → `includeInactive` mostra inativo com
  `deletedAtUtc` → GET 404 → **reuso do CNPJ (201)** → duplicado ativo **400**.
  Dados de smoke removidos do banco.

## Pendências / Etapa 14

- **Sessões/refresh tokens de usuários de um tenant soft-deletado/editado**
  (registrada conforme tarefa 4): hoje o **login/refresh** é bloqueado no
  momento da tentativa, mas os **refresh tokens já emitidos continuam válidos**
  (acessos JWT também, até 15 min). Não há revogação em massa
  (`RevokeAllUserRefreshTokensAsync` por tenant). Decisões futuras:
  - revogar todos os refresh tokens dos usuários do tenant no soft delete;
  - bloquear ações também para `Status = Inactive/Suspended` (hoje apenas
    soft-delete bloqueia) — o registro/inativação por pagamento fica pendente.
- **Etapa 13.5/14 (sugerida)**: aplicar/validar os limites do plano
  (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) no cadastro de filiais/usuários.
