# Etapa 15 — Modules + Planos por Módulo + Vínculo Tenant↔Module

## Objetivo

Introduzir o conceito de **Module** (produto/serviço contratável da plataforma,
ex.: SmartSync Agro/E-Commerce/Agenda) e reorganizar a contratação:

- `Module` (entidade nova) com `slug` **imutável e estável** — é o identificador
  que os **demais microsserviços** usam para saber se o tenant contratou o módulo;
- `Plan` passa a pertencer a **um** módulo (`module_id` **obrigatório**);
- **`Tenant.PlanId` é REMOVIDO** (Etapa 13) e substituído pelo vínculo
  `TenantModule` (`tenant_modules`): um tenant adquire um módulo com um plano,
  com histórico de vigências (billing) e troca de plano (upgrade/downgrade);
- `PlansController` migra de `/api/plans` para aninhado em
  `/api/modules/{moduleId}/plans`;
- Novo `TenantModulesController` em `/api/tenants/{tenantId}/modules`
  (**exclusivo SuperAdmin** — decisão do usuário).

## Decisões do usuário (registradas)

1. **Tarefa 6 — Rota dos planos**: aninhada `api/modules/{moduleId}/plans`
   (preferência do enunciado; consistente com o padrão de branches da Etapa 14).
   A opção flat `/api/plans` foi descartada.
2. **Tarefa 8 — Autorização dos vínculos tenant↔módulo**: **SuperAdmin APENAS**
   por enquanto (Recomendado). TenantAdmin **NÃO** opera vínculos, nem no próprio
   tenant — contratação/billing é sensível e o CRUD de tenants (referência de
   onde ficaria o vínculo) também é SuperAdmin-only. Reverter depois é simples
   (mesmo `IsTenantAccessible` da Etapa 14).

## Modelagem

### Domain

- `ModuleId` / `TenantModuleId`: record structs fortes (padrão `TenantId`/`PlanId`).
- `Module` (`Domain/Entities/Module.cs`):
  - `Name`, **`Slug` (imutável após criação)**, `Description?`, `IsActive`,
    `CreatedAtUtc`, `DeletedAtUtc`;
  - `Update(name, description)` — **não** edita o slug (identidade);
  - `SoftDelete()` idempotente (padrão Tenant/Plan/Branch);
  - validação do slug por regex `^[a-z0-9]+(-[a-z0-9]+)*$` (minúsculas, números,
    hífens; ex.: `agro`, `ecommerce`).
- `Plan` (`Domain/Entities/Plan.cs`): ganha `ModuleId` **obrigatório**
  (`Create(moduleId, name, ...)`, `SetModule` valida default). Unicidade de nome
  passa a ser **por módulo**.
- `Tenant` (`Domain/Entities/Tenant.cs`): **perde `PlanId`** (propriedade, ctor,
  `Create`, `SetPlan`, `Update`, DTOs). O tenant nasce sem módulos e contrata
  depois pelos endpoints de vínculo.
- `TenantModule` (`Domain/Entities/TenantModule.cs`): vínculo com **vigência**:
  `TenantId`, `ModuleId`, `PlanId`, `Status` (`Active=1`/`Inactive=2`),
  `StartDateUtc`, `EndDateUtc?`, `CreatedAtUtc`. Métodos `Create(...)`,
  `SetPlan(...)`, `Inactivate(endUtc)`. Regra central: **no máximo UM vínculo
  ATIVO por (tenant, module)**.

### EF Core

- `ModuleConfiguration`: `modules` (slug/name únicos entre ATIVOS via índice
  parcial `WHERE is_active`), named query filter `"Active"`.
- `PlanConfiguration`: `module_id` (FK Restrict), índice composto parcial
  **`IX_plans_module_id_name_active (module_id, name) WHERE is_active`**
  substituindo o antigo `IX_plans_name_active`.
- `TenantConfiguration`: removidos `plan_id`, FK e `IX_tenants_plan_id`.
- `TenantModuleConfiguration`: `tenant_modules`, FKs Restrict para
  tenant/module/plan, índices por FK e o **índice parcial único**
  `UQ_tenant_modules_tenant_module_active (tenant_id, module_id) WHERE status = 1`
  (garante no banco a regra de um vínculo ativo).
- `IdentityDbContext`: `DbSet<Module>`, `DbSet<TenantModule>`.
- Migration `20260809221315_AddModulesAndTenantModules` (aplicada no boot):
  - **defensiva**: `plans.module_id` nasce NULLABLE → `modules` é criada →
    backfill condicional (`INSERT ... SELECT ... WHERE EXISTS` de planos órfãos
    cria um módulo "Legado" e vincula) → `ALTER` para NOT NULL. Em bancos vazios
    (compose/testes) o `WHERE EXISTS` não encontra nada; em bancos com planos
    pré-existentes, a FK não quebra. (Descoberta em teste: a versão gerada
    quebrava com `42P01 relation "modules" does not exist` porque o SQL rodava
    **antes** do `CreateTable` — a ordenação foi corrigida.)

### Application

- `Modules/` (novo): `CreateModuleCommand(Name, Slug, Description?)`,
  `UpdateModuleCommand(Id, Name, Description?)`, `SoftDeleteModuleCommand(Id)`,
  `ListModulesQuery(Page, PageSize, IncludeInactive)`,
  `GetModuleByIdQuery(Id)`, `ModuleDto`, validators (slug com regex),
  `IModuleService`.
- `Plans/` (refatorado p/ escopo por módulo): todos os commands/queries ganham
  `ModuleId` (rota prevalece); `SoftDeletePlanCommand(ModuleId, Id)`;
  `GetPlanByIdQuery(ModuleId, Id)`; `ListPlansQuery(ModuleId, Page, PageSize,
  IncludeInactive)`; `PlanDto` ganha `ModuleId`; `IPlanService.CreateAsync`
  agora retorna **`PlanDto?`** (null = módulo inexistente/inativo → 404).
- `TenantModules/` (novo): `LinkTenantModuleCommand(TenantId, ModuleId, PlanId)`,
  `UpdateTenantModuleCommand(TenantId, ModuleId, PlanId)`,
  `UnlinkTenantModuleCommand(TenantId, ModuleId)`,
  `ListTenantModulesQuery(TenantId, Page, PageSize, IncludeInactive)`,
  `TenantModuleDto(Id, TenantId, ModuleId, ModuleName, PlanId, Status,
  StartDateUtc, EndDateUtc, CreatedAtUtc)` — o plano é referenciado apenas por
  `PlanId` (FK); o nome é obtido na tabela de planos quando necessário,
  validators, `ITenantModuleService`.
- `Tenants/`: `CreateTenantCommand`, `UpdateTenantCommand` e `TenantDto`
  **perdem `PlanId`**.

### Infrastructure

- `Modules/ModuleService.cs`: valida slug/nome únicos entre ATIVOS
  (`module.slug.duplicate` / `module.name.duplicate`), converte `ArgumentException`
  do slug em 400, soft delete via named query filter.
- `Plans/PlanService.cs`: **todas as operações escopadas por `ModuleId`**
  (`FindActiveAsync(moduleId, planId)` → null → 404; listagem com
  `Where(p => p.ModuleId == moduleId)` **sempre imposto**; unicidade por módulo).
- `TenantModules/TenantModuleService.cs` — semântica definida:
  - **Link (POST)**: tenant deve existir/estar ativo (senão null → 404); módulo
    ativo (400); plano **ativo E do módulo** (400); se já há vínculo ATIVO →
    **400** `tenant_module.active.exists`;
  - **Update (PUT)** — troca de plano: sem vínculo ativo → **cria** novo (reativa);
    mesmo plano → **idempotente** (devolve o vínculo atual); plano diferente →
    `Inactivate(now)` na vigência atual + cria nova (**histórico preservado**);
  - **Unlink (DELETE)**: inativa a vigência ativa (404 se não houver);
  - **List (GET)**: escopado por TenantId; padrão só `Active`; `includeInactive`
    traz histórico; nomes de Module/Plan sempre resolvidos via
    **`IgnoreQueryFilters(["Active"])`** (exibição mesmo de soft-deletados).
- DI: `IModuleService`, `ITenantModuleService` registrados.

### API

- `ModulesController` (`/api/modules`, `[Authorize(Roles = SuperAdmin)]`):
  POST 201, PUT/DELETE/GET por `{id}` com 404, GET list paginada com
  `includeInactive`.
- `PlansController` migrado: `[Route("api/modules/{moduleId:guid}/plans")]`
  (SuperAdmin). POST 201 (404 módulo inexistente), PUT/DELETE/GET por `{id}`
  (404), GET list **deste** módulo. `CreatedAtAction` aponta para GetById com
  `{moduleId, id}`.
- `TenantModulesController`
  (`/api/tenants/{tenantId:guid}/modules`, `[Authorize(Roles = SuperAdmin)]`):
  POST (vincular) 201, PUT `{moduleId}` (trocar plano) 200, DELETE `{moduleId}`
  (desvincular) 204, GET list paginada com `includeInactive`. **Sem claim-based
  `IsTenantAccessible`** — por ser SuperAdmin-only, a rota de tenant é
  irrestrita entre SuperAdmins.

### Testes (111/111 passando)

- `TestData`: `CreateModuleAsync(...)` novo; `CreatePlanAsync(services, moduleId,
  name?)` agora exige o módulo.
- `ModulesTests` (13): CRUD + slug inválido 400, slug/nome duplicados 400,
  slug imutável no PUT, 401/403, soft delete (oculto + `includeInactive` +
  reuso de slug), paginação.
- `PlansTests` (reescrito, 16): rotas aninhadas; módulo inexistente → 404;
  **nome duplicado no MESMO módulo → 400** e **mesmo nome em módulos
  DIFERENTES → 201** (unicidade por módulo); listagem não atravessa módulos;
  editar plano de outro módulo pela rota errada → 404; soft delete; paginação.
- `TenantModulesTests` (15): vínculo 201 com nomes resolvidos, 404 tenant
  inexistente, 400 módulo inexistente / plano de outro módulo / vínculo ativo
  duplicado, **TenantAdmin → 403** (decisão da tarefa 8), 401/403; troca de
  plano (nova vigência + histórico em `includeInactive`), idempotência com mesmo
  plano, reativação via PUT direto; desvincular 204 + inativo no histórico;
  listagem escopada por tenant.
- `TenantsTests` (ajustado): removidos `CriarTenant_ComPlanoAtivo`,
  `CriarTenant_PlanInexistente`, `CriarTenant_PlanInativo` (plano não é mais
  atribuído no tenant); `UpdateTenantCommand`/`TenantDto` sem `PlanId`.
- Descobertas durante a execução:
  - ordenação da migration (ver acima);
  - colisão de dados fixos entre testes da mesma classe (slug `agro` repetido) —
    dados tornados únicos por teste;
  - com a suíte maior, a listagem padrão (`pageSize=20`) pode não conter o item
    deletado → assertivas de `includeInactive` usam `pageSize=100`.

### Docker / OpenAPI / Scalar

- Rebuild + `docker compose up -d --build api`; healthcheck OK. Migration
  aplicada: `modules` e `tenant_modules` criadas, `plans.module_id` presente,
  `tenants.plan_id` removido (conferido via `information_schema`); backfill
  no-op (0 planos pré-existentes).
- OpenAPI: paths `/api/modules`, `/api/modules/{id}`,
  `/api/modules/{moduleId}/plans`, `/api/modules/{moduleId}/plans/{id}`,
  `/api/tenants/{tenantId}/modules`, `/api/tenants/{tenantId}/modules/{moduleId}`;
  schemas `ModuleDto`, `PlanDto` (com `moduleId`), `TenantModuleDto`,
  `Create/Update/Link/UpdateTenantModuleCommand`, `TenantModuleStatus`, etc.;
  `/scalar/v1` responde 200.
- Smoke no container (SuperAdmin de seed): criar módulo → criar plano no módulo
  → criar tenant → **vincular** (201, status Active, nomes resolvidos) → listar
  vínculos (total=1) → listar módulos/planos (total=1).

## Arquivos criados/alterados

- Domain: `Common/ModuleId.cs`, `Common/TenantModuleId.cs`, `Enums/TenantModuleStatus.cs`,
  `Entities/Module.cs`, `Entities/TenantModule.cs` (novos); `Entities/Plan.cs`
  (module_id), `Entities/Tenant.cs` (remove PlanId)
- Infrastructure: `Configurations/ModuleConfiguration.cs`,
  `Configurations/TenantModuleConfiguration.cs` (novos); `Configurations/PlanConfiguration.cs`,
  `Configurations/TenantConfiguration.cs`, `Persistence/IdentityDbContext.cs` (alterados);
  `Migrations/20260809221315_AddModulesAndTenantModules*.cs` (novo)
- Application: `Modules/` e `TenantModules/` (novos); `Plans/*` (refatorado);
  `Tenants/{CreateTenantCommand, UpdateTenantCommand, TenantDto}.cs` (alterados)
- Infrastructure: `Modules/ModuleService.cs`, `TenantModules/TenantModuleService.cs`
  (novos); `Plans/PlanService.cs`, `Tenants/TenantService.cs`,
  `DependencyInjection.cs` (alterados)
- API: `Endpoints/ModulesController.cs`, `Endpoints/TenantModulesController.cs`
  (novos); `Endpoints/PlansController.cs` (rotas aninhadas)
- Testes: `ModulesTests.cs`, `TenantModulesTests.cs` (novos); `PlansTests.cs`
  (reescrito), `TenantsTests.cs` (ajustado), `Lab/TestData.cs` (alterado)

## Comandos executados

- `dotnet build src/Identity.Api/Identity.Api.csproj` → 0 avisos, 0 erros
- `dotnet ef migrations add AddModulesAndTenantModules`
- `dotnet test tests/Identity.Tests/Identity.Tests.csproj` → **111/111 aprovados**
- `docker compose up -d --build api` (migration + smoke + OpenAPI/Scalar)

## Pendências / Próximos passos

- **Limites do plano** (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) agora podem ser
  aplicados consultando o `TenantModule` ATIVO do tenant (pendência das
  Etapas 13/14, ainda não aplicada).
- **Como os outros microsserviços consultam o acesso do tenant a um módulo**:
  os endpoints de vínculo são SuperAdmin-only. Para o tenant (ou os serviços)
  saberem quais módulos o tenant contratou, decidir entre: (a) claim/endpoint
  público `GET /api/tenants/me/modules` (TenantAdmin/Client, autorização por
  claim `tenant_id`); (b) consulta direta ao banco pelo microsserviço (definir
  contrato/rede); (c) propagar para o serviço de billing. Registrado, pendente.
- **Revogação em massa de refresh tokens por tenant** no soft delete do tenant
  (pendência da Etapa 13, não tratada aqui).
- **Cadastro de usuários internos** (Manager/Seller/Delivery/TenantAdmin) — o
  vínculo TenantAdmin→tenant ativaria a regra "só o próprio tenant" da Etapa 14.
