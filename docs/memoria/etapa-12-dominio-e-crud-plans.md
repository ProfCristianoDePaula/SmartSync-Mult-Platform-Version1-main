# Etapa 12 — Domínio e CRUD de Plans

> ⚠️ **Revisão (Etapa 15):** o modelo de `Plan` mudou. A partir da Etapa 15, um
> `Plan` pertence a **um módulo** (`module_id` obrigatório), a unicidade de nome
> passou a ser **por módulo** (`IX_plans_module_id_name_active` no lugar de
> `IX_plans_name_active`) e o CRUD migrou de `/api/plans` para
> `/api/modules/{moduleId}/plans`. Ver `etapa-15-modulos-e-planos-por-modulo.md`.

## Objetivo

Modelar a entidade **Plan** (plano de assinatura da plataforma) e expor o CRUD
completo via `PlansController`. É pré-requisito da Etapa 13: todo Tenant
passará a referenciar um Plan.

## Decisões do usuário (registradas)

1. **Atributos do Plan** (sem `MaxTenants` — decidido por não encaixar no
   domínio em que Tenant referencia exatamente um Plan):
   - `Name` (obrigatório, único entre planos **ativos**)
   - `Description` (opcional)
   - `MonthlyPrice` (`decimal(18,2)`)
   - `AnnualPrice` (`decimal(18,2)`)
   - `TrialDays` (int — período grátis em dias)
   - `Features` (lista — `text[]` no Postgres)
   - `MaxBranches`, `MaxUsers`, `MaxStorageMb` (int)
   - `IsActive` (bool) + `DeletedAtUtc` (soft delete / auditoria)
   - `CreatedAtUtc`
2. **Padrão CQRS**: commands/queries como DTOs + `IPlanService` (sem MediatR),
   consistente com `IAuthService`/`IRoleService`.
3. **Soft delete**: `IsActive + DeletedAtUtc` + **named query filter**
   (`HasQueryFilter("Active", ...)`), abordagem planejada na Etapa 02.

## O que foi feito

### 1. Domain

- `Identity.Domain/Common/PlanId.cs` (record struct forte).
- `Identity.Domain/Entities/Plan.cs`: validações no construtor/setters
  (`ArgumentException`), `Create(...)`, `Update(...)` e `SoftDelete()` (seta
  `IsActive=false` + `DeletedAtUtc`).

### 2. EF Core

- `DbSet<Plan>` no `IdentityDbContext`.
- `PlanConfiguration`:
  - tabela `plans`; `features` como **`text[]`** (array nativo do Npgsql);
  - preços `numeric(18,2)`;
  - índice único **parcial** `IX_plans_name_active` em `name` com
    `HasFilter("\"is_active\"")` → nome único apenas entre ativos;
  - **named query filter** `HasQueryFilter("Active", p => p.IsActive)` — planos
    inativos ficam fora de todas as consultas por padrão;
  - `IgnoreQueryFilters(["Active"])` usado no `ListPlansQuery(IncludeInactive)`.
- Migration `20260809213102_AddPlans` (verificada no Postgres do compose:
  `features=ARRAY`, `numeric(18,2)`, índice parcial).

### 3. Application (commands/queries + FluentValidation)

- Pacotes: `FluentValidation` 12.1.1 + `FluentValidation.DependencyInjectionExtensions`.
- `Identity.Application/DependencyInjection.cs` (novo): `AddApplication()`
  registra os validators via `AddValidatorsFromAssembly`.
- `Identity.Application/Plans/`:
  - `CreatePlanCommand`, `UpdatePlanCommand`, `SoftDeletePlanCommand`,
    `ListPlansQuery` (Page/PageSize/IncludeInactive), `GetPlanByIdQuery`;
  - `PlanDto`, `PagedResult<T>`;
  - `CreatePlanCommandValidator`, `UpdatePlanCommandValidator` (nome
    obrigatório ≤ 100, preços ≥ 0, trial ≥ 0, limites ≥ 1, features sem itens
    vazios);
  - `IPlanService` (Create/Update/SoftDelete/List/GetById).

### 4. Infrastructure

- `Identity.Infrastructure/Plans/PlanService.cs`: valida com FluentValidation
  (`ValidateAndThrowAsync`), garante **nome único entre planos ativos**
  (`upper()` no banco, case-insensitive; lança `BusinessRuleViolationException`
  "plan.name.duplicate"), aplica soft delete, lista paginada (Page ≥ 1, PageSize
  clamp 1–100; `includeInactive` → `IgnoreQueryFilters(["Active"])`).
- DI: `AddScoped<IPlanService, PlanService>()` no `AddInfrastructure`.

### 5. API — `PlansController`

- `[Authorize(Roles = Roles.SuperAdmin)]` em todos os endpoints (403 para as
  demais roles).
- `POST /api/plans` → `201 Created` (Location para GET /api/plans/{id}).
- `PUT /api/plans/{id}` → `200` (id da rota prevalece sobre o body); `404` se
  inexistente/inativo.
- `DELETE /api/plans/{id}` → `204` (soft delete); `404` se inexistente/inativo.
- `GET /api/plans?page=&pageSize=&includeInactive=` → `200` `PagedResult<PlanDto>`.
- `GET /api/plans/{id}` → `200`; `404` (inativos ocultados pelo query filter).
- FluentValidation → `400 ProblemDetails` "Dados inválidos.";
  `BusinessRuleViolationException` → `400 ProblemDetails` "Regra de negócio
  violada.".
- `Program.cs`: `builder.Services.AddApplication()` (validators).

### 6. Testes (45/45 passando)

- `tests/Identity.Tests/PlansTests.cs` (12 testes): 201 criação, 401 sem token,
  403 Client, 400 nome obrigatório, 400 nome duplicado ativo (case-insensitive),
  paginação (2 por página, asserções relativas porque o Postgres é
  compartilhado pela suíte), GET 200/404, PUT 200/400 nome duplicado, DELETE 204
  + oculta da lista + nome reutilizável + 404.
- Correção na revisão: a asserção de `TotalItems`/`TotalPages` absolutos foi
  trocada por asserções relativas (`>=`) com prefixo único por teste, porque o
  Postgres de teste é único para toda a suíte.

### 7. Docker / OpenAPI / Scalar

- Rebuild da imagem `identity-tenants-api` + `docker compose up -d --no-deps api`;
  healthcheck OK. Migration `20260809213102_AddPlans` aplicada no Postgres do
  compose automaticamente pelo `IdentitySeeder` no boot (nenhum comando manual).
- OpenAPI `/openapi/v1.json`: `/api/plans` (POST/GET) e `/api/plans/{id}`
  (PUT/DELETE/GET), com `security: Bearer` no POST e schemas
  `CreatePlanCommand` (request) / `PlanDto` (201).
- `/scalar/v1` responde 200 (app em Development pelo override).
- Smoke no container (SuperAdmin de seed): criar → listar (total=1) → detalhe →
  update (nome/maxUsers alterados) → delete 204 → oculto da lista padrão →
  `includeInactive=true` mostra `isActive=false` + `deletedAtUtc` → GET por id
  404. Dado de smoke removido do banco após a validação.

## Pendências / Etapa 13

- Etapa 13: Tenant referencia um Plan (`tenant.plan_id`) com validação de que
  **apenas planos ativos** podem ser atribuídos. Decisões a confirmar: política
  quando um plano é inativado com tenants vinculados (manter ativo; migrar de
  plano; bloquear novo vínculo apenas?).
