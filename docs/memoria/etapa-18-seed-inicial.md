# Etapa 18 — Seed Inicial da Plataforma (DbSeeder)

## Objetivo

Criar o **bootstrap da plataforma** que roda **uma vez** na inicialização da
API, dentro de um escopo de DI: dados mínimos para operar o Identity & Tenants
desde o primeiro boot — módulo, plano, tenant, filial, vínculo tenant↔módulo e o
SuperAdmin global. Documentar em `docs/SEED-INICIAL.md`, confirmar o login
(Scalar-equivalente) e fechar a memória.

Ordem de criação (com dependências): **Module → Plan → Tenant → Branch →
vínculo Tenant-Module → SuperAdmin**.

## Decisões (registradas)

1. **Convenção `null` = "sem limite"** nos limites do plano. `MaxBranches`,
   `MaxUsers` e `MaxStorageMb` passam de `int` para `int?`: `null` = sem limite;
   valor informado deve ser `≥ 1`. O plano de bootstrap "Full Access" usa `null`
   nos três. (Premissa 1 do usuário confirmada — exigiu mudança de modelo +
   migration.)
2. **Senha do SuperAdmin NUNCA em hardcode**: lida da variável de ambiente
   `SEED_SUPERADMIN_PASSWORD` (fallback de compatibilidade: seção
   `SeedSuperAdmin:Password`). Default de desenvolvimento definido em código,
   usado **apenas em Development**; fora de Development a variável é
   obrigatória e, sem ela, o usuário **não** é criado (aviso nos logs).
   (Premissa 2 do usuário confirmada.)
3. **DbSeeder idempotente**: cada etapa checa a existência antes de criar —
   module por `slug`, plan por nome dentro do módulo, tenant por número do
   documento, filial por nome dentro do tenant, vínculo por `(tenant, module)`
   ativo e usuário por e-mail. Repetir a inicialização nunca duplica dados.
4. **E-mail fixo do SuperAdmin**: `sa@smartsync.com.br` (não configurável),
   usuário **global** (`TenantId` nulo) → JWT **sem** claim `tenant_id`. O mesmo
   e-mail é usado pelo tenant de bootstrap — são **tabelas distintas**
   (`tenants.email` único global vs `users` com índice `(tenant_id, email)`, em
   que `NULL` é distinto), sem conflito.
5. **Separação de responsabilidades dos seeds**: `IdentitySeeder` fica com
   migrations + roles (Etapa 03); `DbSeeder` fica com o bootstrap da plataforma
   + SuperAdmin. Ambos rodam na inicialização, em escopos próprios.
6. **Bootstrap sem dependência de config extra**: os dados da plataforma
   (module/plan/tenant/filial/vínculo) são criados em **qualquer ambiente**;
   apenas a criação do SuperAdmin depende da variável de senha.

## Modelagem

### Domain

- `Entities/Plan.cs`: `MaxBranches`/`MaxUsers`/`MaxStorageMb` → `int?`;
  `SetLimits(int?, int?, int?)` valida "se informado, `≥ 1`"; mensagens de erro
  citam "ou null para 'sem limite'".

### Application

- `Plans/CreatePlanCommand.cs`, `Plans/UpdatePlanCommand.cs`, `Plans/PlanDto.cs`:
  campos de limite → `int?` (documentado como "null = sem limite").
- `Plans/CreatePlanCommandValidator.cs`, `Plans/UpdatePlanCommandValidator.cs`:
  `GreaterThanOrEqualTo(1)` → `Must(limit => limit is null || limit >= 1)` com
  mensagem explicando `null` = sem limite.

### Infrastructure

- `Persistence/Configurations/PlanConfiguration.cs`: removido `.IsRequired()` dos
  três campos de limite (colunas nullable).
- `Seed/DbSeeder.cs` (novo, namespace `Identity.Infrastructure.Seed`):
  - cria scope próprio a partir de `IServiceProvider` recebido;
  - garante as roles (`Roles.All`) via `RoleManager` antes de criar o usuário;
  - cria Module **SmartSync Core** (`slug` = `core`);
  - cria Plan **Full Access** (preço R$ 0,00, `trialDays` 0, limites `null`);
  - cria Tenant **SmartSync Platform** (pessoa física, CPF `295.584.478-03`,
    e-mail `sa@smartsync.com.br`);
  - cria Branch **SmartSync Platform** (Rua Albertina Balthazar de Oliveira,
    911, Bela Vista, Jaú/SP, CEP `17206-441`; contato com o e-mail do tenant —
    telefone é placeholder documentado);
  - cria vínculo `TenantModule` **ativo** tenant ↔ `core` via Full Access;
  - cria o SuperAdmin `sa@smartsync.com.br` via `UserManager.CreateAsync` +
    `AddToRoleAsync` (`EmailConfirmed = true`, `TenantId` nulo);
  - existence-checks usam `IgnoreQueryFilters(["Active"])` (module/plan/tenant/
    branch) para respeitar até dados soft-deletados; `TenantModule` não tem
    filtro "Active" (histórico é mantido), então a query é direta.
- `Auth/IdentitySeeder.cs`: removido o bloco de SuperAdmin via config
  (`SeedSuperAdmin:Email/Password`) — passa a cuidar só de migrations + roles.
- `Seed/DbSeeder` resolve a senha com: `SEED_SUPERADMIN_PASSWORD` →
  `SeedSuperAdmin:Password` → default de dev (só Development) → senão pula com
  aviso.

### API

- `Program.cs`: `IdentitySeeder.SeedAsync(app.Services)` →
  `DbSeeder.SeedAsync(app.Services)` (usings de `Identity.Infrastructure.Seed`).

### Migration

`20260809232450_MakePlanLimitsNullable`: `AlterColumn` (NOT NULL → NULL) de
`max_branches`, `max_users`, `max_storage_mb` em `plans`. Sem backfill (valores
existentes preservados).

### Config / documentação (transição `admin@identity.local` → `sa@smartsync.com.br`)

- `docker-compose.yml`: `SeedSuperAdmin__Email/Password` → **`SEED_SUPERADMIN_PASSWORD`**
  (`${SEED_SUPERADMIN_PASSWORD:?Defina SEED_SUPERADMIN_PASSWORD no .env}`).
- `.env.example`: `SUPERADMIN_EMAIL`/`SUPERADMIN_PASSWORD` →
  **`SEED_SUPERADMIN_PASSWORD`** (e-mail fixo documentado, sem variável).
- `README.md`: senha do `.env`, texto de migrations/seed e curl de login.
- `docs/DEV-CREDENTIALS.md`: tabela do SuperAdmin, curl de login e lembrete de
  segurança.
- `docs/CONTRATO-IDENTIDADE.md`: §3.1 (SuperAdmin via DbSeeder) e §14.1 (curl de
  login).
- `docs/SEED-INICIAL.md` (novo): ordem, chaves de idempotência, convenção
  `null` = sem limite, uso de `SEED_SUPERADMIN_PASSWORD` e como confirmar.

## Testes (118/118 passando, eram 115)

- `tests/Identity.Tests/BootstrapSeederTests.cs` (novo, collection "integration"):
  - `Seed_CriaDadosDaPlataforma` — module `core`, plan Full Access (preço 0,
    limites `null`), tenant Fisica CPF `29558447803`, branch com CEP, vínculo
    ativo e SuperAdmin (TenantId nulo, e-mail confirmado, role SuperAdmin);
  - `Seed_Repetido_DeveSerIdempotente` — roda `DbSeeder.SeedAsync` uma segunda
    vez e confirma que nada duplica;
  - `Login_SuperAdminBootstrap_TokenSemTenantId` — login
    `sa@smartsync.com.br` funciona e o payload do JWT **não** contém
    `tenant_id` (decodificado manualmente via base64url).
- `tests/Identity.Tests/PlansTests.cs`: asserts de limites ajustados para
  `Assert.Equal<int?>(...)` (campos agora nullable).
- Sem quebras: asserts de contagem dos demais testes são relativos (`>= 3`) ou
  escopados por tenant/módulo — o seed não os afeta.

## Comandos executados

- `dotnet ef migrations add MakePlanLimitsNullable --project src/Identity.Infrastructure --startup-project src/Identity.Api --output-dir Persistence/Migrations`
- `dotnet test tests/Identity.Tests/Identity.Tests.csproj` → **118/118 aprovados**
- Verificação local (equivalente ao Scalar): Postgres isolado (`docker run`,
  porta 55432) + `dotnet run` em Development → login `sa@smartsync.com.br` →
  token sem `tenant_id`, role `SuperAdmin`, e confirmação via API dos dados do
  seed (module `core`, plano Full Access com limites `null`, tenant, filial e
  vínculo ativo). Ambiente de verificação removido ao final.

## Pendências / Próximos passos

- `GET /api/tenants/me/modules` (contrato recomendado da Etapa 16, §12) segue
  **pendente**.
- Limites do plano ainda não são **aplicados** no cadastro de filiais/usuários
  (agora com suporte a `null` = sem limite, a aplicação pode ignorar limites
  nulos).
- Revogação em massa de refresh tokens por tenant no soft delete (Etapa 13).
- Documento do **usuário** (`ApplicationUser.Document`) segue como string livre
  (CPF/CNPJ por role, Etapa 03) — não unificado com o VO `Documento` do tenant.
