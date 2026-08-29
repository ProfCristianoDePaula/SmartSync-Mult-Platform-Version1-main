# Etapa 14 — CRUD de Filiais (Branches) escopado ao Tenant

## Objetivo

Expor o CRUD completo de **Filiais (Branches)** sempre aninhado em um tenant
(`/api/tenants/{tenantId}/branches`), reutilizando o domínio modelado na Etapa 02.
Inclui: soft delete no Branch (não existia), autorização **por claim do JWT**
(SuperAdmin global em qualquer tenant; TenantAdmin **apenas no próprio tenant**),
e a garantia de que listagem/detalhe **nunca vazam** filiais de outro tenant.

## Regras de autorização (decisão registrada)

O JWT só carrega a claim `tenant_id` quando o usuário tem `TenantId`
(`TokenService` emite a claim condicionalmente). A leitura da regra
"SuperAdmin qualquer tenant; TenantAdmin só o próprio" é **driven by claim**:

- **Sem claim `tenant_id`** (SuperAdmin, usuário global) → pode operar em
  **qualquer** `{tenantId}` da rota.
- **Com claim `tenant_id`** (TenantAdmin vinculado ao tenant) → o `{tenantId}` da
  rota **deve bater** com a claim; caso contrário `403 Forbidden`.

> ⚠️ Observação de modelo: a Etapa 04 registrava TenantAdmin como usuário
> **global** (TenantId nulo). Para a Etapa 14, um TenantAdmin precisa estar
> vinculado ao tenant que administra (TenantId não-nulo → claim `tenant_id` no
> JWT). A validação é 100% por claim, então a regra passa a valer automaticamente
> quando esse vínculo existir. Nenhuma alteração foi feita no modelo de usuários
> nesta etapa; a autorização não confia jamais **apenas** no `{tenantId}` da rota.

## O que foi feito

### 1. Domain

- `Branch` (Etapa 02 + soft delete):
  - novas propriedades `IsActive` + `DeletedAtUtc`;
  - `Create(...)` inicializa `IsActive = true`;
  - novo método `Update(name, address, contact)` (consistente com `Tenant.Update`);
  - `SoftDelete()` (idempotente: já inativa não altera nada).

### 2. EF Core

- `BranchConfiguration`:
  - `is_active` (`boolean`, **default `true`** — igual ajuste do Tenant na Etapa 13,
    evita filiais pré-existentes nascerem inativas no backfill);
  - `deleted_at_utc`;
  - **named query filter** `HasQueryFilter("Active", b => b.IsActive)` — filiais
    inativas ficam fora de todas as consultas por padrão; a listagem quebra com
    `IgnoreQueryFilters(["Active"])` para `includeInactive=true`.
- Migration `20260809215735_AddBranchSoftDelete` (aplicada automaticamente no
  boot pelo `IdentitySeeder`; conferido no Postgres do compose: colunas +
  default).

### 3. Application — `Branches/`

- Commands/queries como DTOs (padrão CQRS sem MediatR, consistente com Plans/Tenants):
  - `CreateBranchCommand(TenantId, Name, Address, Contact)`;
  - `UpdateBranchCommand(TenantId, BranchId, Name, Address, Contact)`;
  - `SoftDeleteBranchCommand(TenantId, BranchId)`;
  - `ListBranchesByTenantQuery(TenantId, Page, PageSize, IncludeInactive)`;
  - `GetBranchByIdQuery(TenantId, BranchId)`.
- `BranchAddress` / `BranchContact` (records de entrada/saída; endereço e contato
  aninhados no body da API e no DTO).
- `BranchDto(Id, TenantId, Name, Address, Contact, IsActive, DeletedAtUtc)`.
- `CreateBranchCommandValidator` / `UpdateBranchCommandValidator` (FluentValidation):
  espelham as regras dos value objects `Address`/`Contact` do Domain (logradouro,
  UF com 2 letras, CEP 8 dígitos, telefone 10/11, e-mail) — validação defensiva;
  o serviço converte `ArgumentException` dos VOs em `400` (nunca 500).
- `IBranchService` (Create/Update/SoftDelete/ListByTenant/GetById). O TenantId é
  sempre parâmetro explícito.

### 4. Infrastructure

- `Branches/BranchService.cs`:
  - valida com FluentValidation; converte `ArgumentException` dos VOs em
    `BusinessRuleViolationException` (`branch.address.invalid` /
    `branch.contact.invalid`);
  - **Create**: exige tenant existente e ATIVO (query filter "Active" oculta
    soft-deletados) → senão retorna null → `404` na API;
  - **Update/SoftDelete/GetById**: busca SEMPRE escopada por
    `TenantId + BranchId` com o query filter ativo → null → `404`;
  - **List**: `Where(b => b.TenantId == tenantId)` **sempre imposto** + paginação
    (Page ≥ 1, PageSize clamp 1–100) + `OrderBy(Name)`; `includeInactive` →
    `IgnoreQueryFilters(["Active"])`. A query nunca atravessa tenants.
- DI: `AddScoped<IBranchService, BranchService>()`.

### 5. API — `BranchesController`

- Rota aninhada `[Route("api/tenants/{tenantId:guid}/branches")]`.
- `[Authorize(Roles = SuperAdmin + "," + TenantAdmin)]` no nível do controller
  (demais roles → `403`).
- `IsTenantAccessible(routeTenantId)`: lê `User.FindFirstValue(JwtClaims.TenantId)`;
  sem claim → global (SuperAdmin) → libera; com claim → precisa `Guid` igual ao da
  rota, senão `403 ProblemDetails "Acesso negado a este tenant."`. Aplicado em
  **todas** as ações, antes de tocar no serviço.
- `POST /api/tenants/{tenantId}/branches` → `201 Created` (Location GET .../{branchId});
  `404` se o tenant não existir/inativo.
- `PUT /api/tenants/{tenantId}/branches/{branchId}` → `200`; `404`.
- `DELETE /api/tenants/{tenantId}/branches/{branchId}` → `204` (soft delete); `404`.
- `GET /api/tenants/{tenantId}/branches?page&pageSize&includeInactive` → `200`
  `PagedResult<BranchDto>`.
- `GET /api/tenants/{tenantId}/branches/{branchId}` → `200`; `404` (inativas
  ocultadas pelo query filter; filial de OUTRO tenant sob este caminho também dá
  `404` — sem vazamento nem para SuperAdmin).
- Tratamento de erros igual a Tenants/Plans: `ValidationException` → `400` "Dados
  inválidos."; `BusinessRuleViolationException` → `400` "Regra de negócio violada.".

### 6. Testes (82/82 passando)

- `tests/Identity.Tests/BranchesTests.cs` (18 testes novos):
  - criação 201 (SuperAdmin global), 404 tenant inexistente / tenant soft-deletado,
    401 sem token, 403 Client;
  - **autorização por claim**: TenantAdmin **do próprio** tenant → 201; TenantAdmin
    **de outro** tenant → **403**;
  - **vazamento**: listagem de A não contém filiais de B; TenantAdmin de A só vê o
    próprio tenant e recebe **403** ao listar B; detalhe de filial de A sob o
    caminho de B → **404**;
  - paginação (2 por página, ordem, `TotalItems` exato do tenant isolado);
  - GET 200/404; PUT 200/404; DELETE 204 + oculto da lista + `includeInactive`
    (`IsActive=false` + `DeletedAtUtc`) + GET 404 + segundo delete 404.
- Suíte anterior continua verde: **82/82** (64 anteriores + 18 novos).

### 7. Docker / OpenAPI / Scalar

- Rebuild + `docker compose up -d --build api`; healthcheck OK. Migration
  `AddBranchSoftDelete` aplicada automaticamente (colunas `is_active` default
  `true` + `deleted_at_utc` confirmadas via `information_schema`).
- OpenAPI `/openapi/v1.json`: paths `/api/tenants/{tenantId}/branches` e
  `/api/tenants/{tenantId}/branches/{branchId}`, schemas `CreateBranchCommand`,
  `UpdateBranchCommand`, `BranchDto`, `BranchAddress`, `BranchContact`,
  `PagedResultOfBranchDto`; `/scalar/v1` responde 200.
- Smoke no container (SuperAdmin de seed): criar tenant → criar filial (201, tenant
  correto, `isActive=true`) → listar (total=1) → detalhe (CEP normalizado) → update
  (nome + telefone secundário) → delete 204 → oculto da lista → `includeInactive`
  mostra `isActive=false` + `deletedAtUtc` → GET 404. Tenant de smoke
  soft-deletado ao final (banco limpo).

## Arquivos criados/alterados

- `src/Identity.Domain/Entities/Branch.cs` (soft delete + Update)
- `src/Identity.Infrastructure/Persistence/Configurations/BranchConfiguration.cs`
- `src/Identity.Infrastructure/Persistence/Migrations/20260809215735_AddBranchSoftDelete*.cs`
- `src/Identity.Application/Branches/` (novo): `{BranchAddress, BranchContact,
  CreateBranchCommand, UpdateBranchCommand, SoftDeleteBranchCommand,
  ListBranchesByTenantQuery, GetBranchByIdQuery, BranchDto,
  CreateBranchCommandValidator, UpdateBranchCommandValidator, IBranchService}.cs`
- `src/Identity.Infrastructure/Branches/BranchService.cs` (novo)
- `src/Identity.Infrastructure/DependencyInjection.cs` (registro do serviço)
- `src/Identity.Api/Endpoints/BranchesController.cs` (novo)
- `tests/Identity.Tests/BranchesTests.cs` (novo)

## Comandos executados

- `dotnet build Identity.slnx` → 0 avisos, 0 erros
- `dotnet ef migrations add AddBranchSoftDelete`
- `dotnet test tests/Identity.Tests` → **82/82 aprovados** (18 novos + 64 anteriores)
- `docker compose up -d --build api` (migration + smoke + OpenAPI/Scalar)

## Pendências / Próximos passos

- **Limites do plano** (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) no cadastro de
  filiais/usuários — pendência registrada na Etapa 13, **ainda não aplicada**.
  Agora que o CRUD de filiais existe, `MaxBranches` pode ser validado no
  `CreateBranch` consultando o plano do tenant.
- **Revogação em massa de refresh tokens por tenant** no soft delete do tenant
  (pendência da Etapa 13, não tratada aqui).
- **Cadastro de usuários internos** (Manager/Seller/Delivery/TenantAdmin) —
  os endpoints de admin ainda não existem; quando criados, o vínculo
  TenantAdmin→tenant (TenantId) passa a ativar a regra "só o próprio tenant"
  desta etapa de forma natural.
