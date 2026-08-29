# Seed Inicial da Plataforma (Etapa 18)

> Bootstrap mínimo da plataforma SmartSync criado uma única vez na inicialização
> da API, dentro de um escopo de DI, pelo `DbSeeder` (infraestrutura). É
> **idempotente**: repetir a inicialização (reinício, novo container, ambiente de
> teste) nunca duplica dados.

## Ordem de criação

Cada passo **checa a existência antes de criar** (chave de idempotência entre
parênteses). A ordem segue as dependências: Module → Plan → Tenant → Branch →
vínculo Tenant-Module → SuperAdmin.

| # | Entidade | Dados criados | Chave de idempotência |
|---|----------|---------------|------------------------|
| 1 | `Module` | **SmartSync Core** (`slug` = `core`) | `slug` |
| 2 | `Plan` (no módulo `core`) | **Full Access** — preço R$ 0,00, `trialDays` 0, limites `null` (sem limite) | nome dentro do módulo |
| 3 | `Tenant` | **SmartSync Platform** — pessoa física, CPF `295.584.478-03`, e-mail `sa@smartsync.com.br` | número do documento |
| 4 | `Branch` (do tenant acima) | **SmartSync Platform** — Rua Albertina Balthazar de Oliveira, 911, Bela Vista, Jaú/SP, CEP `17206-441`; contato com o e-mail do tenant | nome dentro do tenant |
| 5 | `TenantModule` | vínculo **ativo** Tenant "SmartSync Platform" ↔ Module `core` via Plan **Full Access** | `(tenant, module)` ativo |
| 6 | `ApplicationUser` | **SuperAdmin** `sa@smartsync.com.br` (usuário **global**, `TenantId` nulo, e-mail confirmado, role `SuperAdmin`) | e-mail |

> O `IdentitySeeder` roda antes e é responsável por aplicar as migrations e
> garantir as roles da plataforma (`Roles.All`). O `DbSeeder` também assegura as
> roles antes de criar o usuário.

## Convenção: limites `null` = "sem limite"

Os campos `maxBranches`, `maxUsers` e `maxStorageMb` de um plano são **nullable**
(migration `MakePlanLimitsNullable`):

- `null` → **sem limite** (usado pelo plano de bootstrap "Full Access");
- valor → deve ser **≥ 1**.

A validação (Domain `Plan.SetLimits` e validators FluentValidation da
Application) rejeita valores `< 1`, mas aceita `null`.

## Senha do SuperAdmin de bootstrap

- A senha vem da variável de ambiente **`SEED_SUPERADMIN_PASSWORD`** (nunca
  hardcode). Em Development, se a variável não estiver definida, o DbSeeder usa a
  senha padrão de desenvolvimento (definida em código).
- **Fora de Development a variável é obrigatória**: sem ela o usuário SuperAdmin
  **não** é criado e um aviso é registrado nos logs.
- **Recomendação:** troque a senha padrão no **primeiro login**, principalmente em
  ambientes compartilhados/produção.
- Compatibilidade: a antiga seção `SeedSuperAdmin:Password` continua sendo lida
  como fallback durante a transição.

## Como confirmar

1. Suba a API e faça login do SuperAdmin:

   ```bash
   curl -s -X POST http://localhost:8080/api/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"identifier":"sa@smartsync.com.br","password":"SUA_SENHA_DO_ENV"}'
   ```

2. O `accessToken` **não deve conter a claim `tenant_id`** (usuário global) —
   decodifique o payload JWT e confirme. O login funciona via Scalar
   (`/scalar/v1`) com as mesmas credenciais.

3. Opcionalmente confira os dados criados consultando a API com o token:
   - `GET /api/modules?pageSize=100` → contém `core`;
   - `GET /api/modules/{coreId}/plans` → contém `Full Access` com limites `null`;
   - `GET /api/tenants?search=SmartSync` → tenant "SmartSync Platform";
   - `GET /api/tenants/{tenantId}/branches` → filial "SmartSync Platform";
   - `GET /api/tenants/{tenantId}/modules` → vínculo ativo com o módulo `core`.

## Onde está implementado

- `src/Identity.Infrastructure/Seed/DbSeeder.cs` — o seed de bootstrap (Etapa 18).
- `src/Identity.Infrastructure/Auth/IdentitySeeder.cs` — migrations + roles (Etapa 03).
- `src/Identity.Api/Program.cs` — `IdentitySeeder.SeedAsync` → `DbSeeder.SeedAsync`
  na inicialização, dentro de escopos de DI.
- Testes de bootstrap: `tests/Identity.Tests/BootstrapSeederTests.cs`.
