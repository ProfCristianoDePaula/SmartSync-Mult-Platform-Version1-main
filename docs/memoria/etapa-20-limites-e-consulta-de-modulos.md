# Etapa 20 — Consulta de Módulos pelo Tenant e Aplicação dos Limites do Plano

## Objetivo

Etapa de **fechamento de pendências** registradas nas Etapas 13/14/15/16/17/18/19
(INDEX): implementa a consulta de módulos do próprio tenant
(`GET /api/tenants/me/modules`, contrato §12.2 do CONTRATO-IDENTIDADE), **aplica
os limites do plano** (filias via `MaxBranches`; `MaxUsers` para usuários
internos) e **revoga em massa os
refresh tokens** dos usuários no soft delete do tenant. Agrupa:

1. **`GET /api/tenants/me/modules`** — endpoint autenticado por claim `tenant_id`
   que responde "quais módulos este tenant contratou" (pendência retomada das
   Etapas 15/16/17);
2. **Limites do plano aplicados** (`MaxBranches` no cadastro de filiais;
   `MaxUsers` reservado para usuários internos — **Client não conta**), com a
   regra de **soma dos planos ativos** (decisão do usuário);
3. **Revogação em massa de refresh tokens por tenant** no soft delete do tenant
   (pendência da Etapa 13).

Nenhuma mudança de escopo de negócio além das pendências registradas.

## Decisões (registradas)

1. **`GET /api/tenants/me/modules`** (contrato §12.2): **qualquer role
   autenticada** que tenha a claim `tenant_id` consulta os módulos ativos do
   SEU tenant. O `TenantId` vem **exclusivamente da claim** (nunca de query
   param) — não há risco de vazar o catálogo de outro tenant. Usuário **sem** a
   claim (ex.: SuperAdmin global) recebe **403** ("Usuário sem tenant
   vinculado"). Retorna só vínculos `Active` e **módulos não soft-deletados**
   (`slug`, `name`, `plan` (nome), `status` = `"active"`, `startDateUtc`,
   `endDateUtc?`). É o endpoint que os demais microsserviços usam em runtime
   para validar acesso/limites sem depender de token de SuperAdmin.
2. **Limite efetivo do plano = SOMA dos limites dos planos dos vínculos ATIVOS**
   (decisão do usuário ao fechar a pendência). Se **QUALQUER** plano ativo tiver
   limite `null` ("sem limite", convenção da Etapa 18), o efetivo é `null`.
   Tenant **sem vínculo ativo** = sem limite contratado (não bloqueia). Planos
   soft-deletados não contam (o query filter `Active` do `Plan` os exclui).
   Novo serviço `IPlanLimitResolver`/`PlanLimitResolver` (Infrastructure,
   registrado no DI) usado pelo `BranchService` (`MaxBranches`); o `MaxUsers`
   fica disponível para o futuro endpoint de usuários internos.
3. **Aplicação no cadastro de filiais** (`BranchService.CreateAsync`): quando o
   `MaxBranches` efetivo é um número e o tenant já atingiu o limite, lança
   `BusinessRuleViolationException` → **400** ("Limite de filiais do plano
   atingido (N).").
4. **`MaxUsers` NÃO conta usuários públicos (Client)** — **correção do usuário
   após a primeira implementação**: o cadastro público (`POST
   /api/auth/register`, autosserviço) cria sempre um `Client` e **nunca é
   barrado por `MaxUsers`** (a checagem no `AuthService.RegisterAsync` foi
   removida). O limite vale apenas para **usuários internos**
   (TenantAdmin/Manager/Seller/Delivery), quando existir o endpoint de criação
   de usuários internos (aplicação futura contando somente usuários de roles
   ≠ `Client`).
5. **Soft delete do tenant revoga refresh tokens em massa**: novo
   `TokenService.RevokeAllTenantRefreshTokensAsync(tenantId)` inativa (no banco)
   todos os refresh tokens ativos dos usuários vinculados ao tenant, dentro da
   **mesma transação** do soft delete (`TenantService.SoftDeleteAsync`). Mesmo
   que o tenant volte a existir, sessões antigas não são recuperáveis.
6. **`MaxStorageMb` segue sem aplicação** — não há upload de arquivos na
   plataforma ainda; continua pendente para a feature que consumir
   armazenamento.

## Mudanças

### Consulta de módulos pelo tenant

- `src/Identity.Application/TenantModules/TenantModuleView.cs` (novo) e
  `TenantModulesView.cs` (novo) — DTOs do contrato §12.2 (`Items`).
- `ITenantModuleService`/`TenantModuleService`: `ListActiveForTenantAsync` —
  vínculos ativos com `slug`/`name` do módulo e `name` do plano; módulos
  soft-deletados excluídos (via `IsActive`, com `IgnoreQueryFilters` explícito).
- `src/Identity.Api/Endpoints/MyTenantModulesController.cs` (novo) — rota
  `GET api/tenants/me/modules`, `[Authorize]`, claim `tenant_id` obrigatória.
- Testes (`TenantModulesTests`): 200 com slug/plano, 401 sem token, 403 sem
  claim (SuperAdmin global), TenantAdmin vê só o próprio tenant, módulo
  soft-deletado não aparece.

### Limites do plano

- `src/Identity.Application/Plans/PlanLimits.cs` + `IPlanLimitResolver.cs`
  (novos) e `src/Identity.Infrastructure/Plans/PlanLimitResolver.cs` (novo);
  registro em `DependencyInjection.cs`.
- `BranchService.CreateAsync` — checagem de `MaxBranches`.
- `AuthService.RegisterAsync` — **sem checagem de `MaxUsers`** (Client não
  conta; correção do usuário). O `IPlanLimitResolver` fica disponível no DI
  para o futuro endpoint de usuários internos.
- Testes (`BranchesTests`): limite atingido → 400; `null` = sem limite; soma de
  planos ativos. Testes (`RegisterTests` em `AccountManagementTests.cs`):
  Clientes NÃO contam para `MaxUsers` (3 registros com plano `MaxUsers=1` → 3×
  201); `null` = sem limite.

### Revogação em massa de refresh tokens

- `TokenService.RevokeAllTenantRefreshTokensAsync` (novo) — join
  `refresh_tokens × users` por `TenantId`.
- `TenantService.SoftDeleteAsync` — transação (soft delete + revogação).
- Teste (`TenantsTests`): refresh token ativo antes, `RevokedAtUtc` preenchido
  após o soft delete, e refresh do token → 401.

## Testes

- `dotnet build Identity.slnx` → **0 avisos, 0 erros**.
- Suítes executadas antes do bloqueio do ambiente: **TenantModulesTests 20/20**
  (inclui os 5 novos de `/me/modules`) e **Branches+Register+TenantModules
  46/46** (inclui os limites de filiais; register validado como não limitado
  por `MaxUsers`).
- **Bloqueio de ambiente (não é do código):** ao reconstruir o binário de
  testes (após `dotnet clean`), a **política de Controle de Aplicativo do
  Windows (Smart App Control, `VerifiedAndReputablePolicyState=1`)** passou a
  bloquear o carregamento do `Identity.Tests.dll` (e até de binários NuGet
  inalterados como `xunit.runner.utility`) com `0x800711C7`. Build 100% OK;
  assemblies de produção (`Identity.Api.dll`, `Identity.Infrastructure.dll`)
  carregam normalmente. A execução da suíte completa fica pendente de ajuste da
  política (desativar Smart App Control / whitelist) ou de uma máquina sem a
  política.

## Pendências / Próximos passos

- **Rodar a suíte completa** (`dotnet test tests/Identity.Tests`) após ajustar a
  política de Controle de Aplicativo no ambiente (bloqueio da Etapa 20).
- **`MaxStorageMb`**: aplicar quando existir feature de armazenamento.
- **`ApplicationUser.Document`** segue como string livre (CPF/CNPJ por role,
  Etapa 03) — não unificado com o VO `Documento` do tenant.
- **Teste end-to-end real** (SSO Google/Facebook, SMS, e-mail) pendente das
  credenciais externas.
