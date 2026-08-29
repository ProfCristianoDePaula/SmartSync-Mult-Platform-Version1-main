# Etapa 11 — Gestão Completa da Conta (reset, logout, sessões, cadastro)

## Objetivo

Finalizar o **controle total da conta** para **todas as roles** (SuperAdmin,
TenantAdmin, Manager, Seller, Delivery, Client): reset de senha por e-mail e por
SMS, troca de senha autenticada, logout (sessão única e geral), revisão do
refresh-token com rotação obrigatória, documentação das expirações de cada token
e — questão levantada pelo usuário — o **endpoint de cadastro (`register`)** que
não existia.

## Por que não havia endpoint `register`?

Registrado e confirmado nas memórias anteriores (Etapa 06):
> "NÃO criar endpoint de cadastro do Client nesta etapa — apenas a
> infraestrutura de confirmação... O cadastro em si fica para etapa futura."

As etapas 03/05 criam usuários internamente (seed do SuperAdmin e SSO do Client),
mas **não existia nenhum fluxo público de autocrédito**. Esta etapa preenche o
gap com **`POST /api/auth/register`** — autosserviço **Client**, confirmado com o
usuário: escopo Client + identificação do tenant via **`tenantId` no body**.

## O que foi feito

### 1. Recuperação de senha (público, resposta neutra)

- `POST /api/auth/forgot-password` — `GeneratePasswordResetTokenAsync` (token
  nativo do Identity) + link por `IEmailSender` usando
  `Email:PasswordResetUrlTemplate` (novo template). Resposta **neutra** (não
  vaza existência). Rate limit `auth-recovery` (3/min por IP).
- `POST /api/auth/forgot-password/sms` — gera **código de 6 dígitos**
  (`GenerateNumericCode`, base em `RandomNumberGenerator`, sem viés),
  persiste **apenas o hash SHA-256** na nova tabela `password_reset_codes`
  (expiração **10 min** — `PasswordResetCodeConstants.Lifetime`), invalida
  códigos anteriores do usuário e envia por `ISmsSender`.
- `POST /api/auth/reset-password` — aceita **token do e-mail** OU **código SMS
  de 6 dígitos**; no fluxo SMS valida o hash/expiração, marca `UsedAtUtc` e
  gera um token nativo na hora para `ResetPasswordAsync` (políticas de senha do
  provider). Em **ambos** os fluxos, após sucesso: **revoga todos os refresh
  tokens do usuário** (reset de senha é sensível).

### 2. Troca de senha autenticada e logout

- `POST /api/auth/change-password` — `[Authorize]` (qualquer role), valida a
  senha atual via `ChangePasswordAsync` e **revoga todas as sessões**.
- `POST /api/auth/logout` — `[Authorize]`; recebe o refresh token e o **revoga
  apenas se pertencer ao usuário autenticado** (`TokenService.RevokeRefreshTokenAsync`).
- `POST /api/auth/logout-all` — `[Authorize]`; revoga **todos** os refresh
  tokens ativos do usuário (`RevokeAllUserRefreshTokensAsync`).

### 3. Estratégia de armazenamento/revogação de refresh tokens (consolidada)

- **Armazenamento**: tabela `refresh_tokens` com **hash SHA-256** do valor
  (`TokenService.Hash`), `UserId`, `ExpiresAtUtc`, `RevokedAtUtc`,
  `ReplacedByTokenHash`; índices únicos `IX_refresh_tokens_token_hash` e
  `IX_refresh_tokens_user_id`.
- **Rotação (obrigatória)**: `refresh-token` gera um novo e revoga o anterior
  (`RotateRefreshTokenAsync` marca `RevokedAtUtc` + `ReplacedByTokenHash`);
  reuso do antigo → `401`.
- **Revogação**: por sessão (`logout`) ou em massa (`logout-all`,
  `change-password`, `reset-password`).
- **Estratégia escolhida** (a documentar como pendência): token imutável
  **no banco com revogação por logout** — NÃO usamos denylist em memória/Redis
  (o JWT continua stateless; a invalidação é do refresh, que é o que importa
  para "encerrar sessões").

### 4. Expiração de cada token (documentada no CONTRATO)

| Token | Expiração |
|-------|-----------|
| Access JWT | 15 min (`Jwt:AccessTokenLifetimeMinutes`) |
| Refresh | 7 dias (`Jwt:RefreshTokenLifetimeDays`) + rotação |
| Reset por e-mail | 30 min (`DataProtectionTokenProviderOptions.TokenLifespan`) |
| Código SMS de reset | 10 min (`PasswordResetCodeConstants.Lifetime`) |
| Confirmação de e-mail | 30 min (mesmo provider) |
| Código de celular (SMS) | default do Identity (provider `Phone`) |
| Cookie externo OAuth | 10 min (handshake) |

### 5. `POST /api/auth/register` (novo — cadastro público de Client)

- `RegisterRequest { FullName, Email, Password, TenantId, Document? }` →
  `AuthService.RegisterAsync`:
  - valida tenant existente;
  - aplica `UserUniquenessValidator` (e-mail + documento únicos **por tenant**,
    Etapa 04);
  - cria `ApplicationUser` com role **Client**, `EmailConfirmed=false`,
    `Document` normalizado (só dígitos), `LockoutEnabled=true`;
  - envia o e-mail de confirmação (reusa `SendConfirmationEmailAsync` da Etapa 06);
  - **não emite tokens** (login exige e-mail confirmado) — retorna `201 Created`.
- `RegisterResult` (Succeeded/Errors) mantém a Application desacoplada de HTTP.
- Rate limit `auth-register` (10/min por IP, configurável).

### 6. Rate limiting novo

- Política `auth-recovery` (forgot-password*, reset-password): **3/min** default.
- Política `auth-register`: **10/min** default.
- Ambas configuráveis via `RateLimiting:PasswordRecovery` / `RateLimiting:Register`
  (padrão da Etapa 07); testes sobrescrevem para 1000/3600.

## Testes executados

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `dotnet build Identity.slnx` | ✅ 0 erros / 0 warnings |
| 2 | `dotnet test` (Testcontainers Postgres) | ✅ **33/33 passando** (31 anteriores + 2 do catálogo de roles) |
| 3 | Reset por e-mail end-to-end (forgot → token → reset → login novo) | ✅ teste passando |
| 4 | Reset por SMS (código 6 dígitos → reset → login) | ✅ teste passando |
| 5 | Reset com código inválido / e-mail inexistente | ✅ 400 / resposta neutra |
| 6 | Change-password (valida senha atual; revoga sessão) | ✅ teste passando |
| 7 | Logout revoga refresh; logout-all revoga todas | ✅ testes passando |
| 8 | Register: cria Client não confirmado + envia e-mail | ✅ teste passando |
| 9 | Register: e-mail duplicado no tenant / tenant inexistente | ✅ 400 |

## Arquivos criados/alterados

- `src/Identity.Application/Auth/{RegisterRequest,RegisterResult}.cs` (novos)
- `src/Identity.Application/Auth/IAuthService.cs` (+ `RegisterAsync`)
- `src/Identity.Application/Notifications/EmailOptions.cs` (+ `PasswordResetUrlTemplate`)
- `src/Identity.Infrastructure/Auth/AuthService.cs` (forgot/reset/change/logout/
  logout-all/register + `UserUniquenessValidator` no ctor)
- `src/Identity.Infrastructure/Persistence/Identity/PasswordResetCode.cs` (novo)
- `src/Identity.Infrastructure/Persistence/Configurations/PasswordResetCodeConfiguration.cs` (novo)
- `src/Identity.Infrastructure/Persistence/IdentityDbContext.cs` (+ `DbSet<PasswordResetCode>`)
- Migration `20260808185411_AddPasswordResetCodes`
- `src/Identity.Infrastructure/Security/TokenService.cs` (+ `RevokeRefreshTokenAsync`,
  `RevokeAllUserRefreshTokensAsync`)
- `src/Identity.Api/Endpoints/AuthController.cs` (+ register, forgot*, reset,
  change, logout, logout-all)
- `src/Identity.Api/Program.cs` (+ rate limits `auth-recovery`/`auth-register`)
- `src/Identity.Api/appsettings.json` (+ `RateLimiting:PasswordRecovery`, `:Register`)
- `src/Identity.Infrastructure/DependencyInjection.cs` (+ lifespan 30 min dos
  tokens de reset/confirmação de e-mail via `DataProtectionTokenProviderOptions`)
- `src/Identity.Application/RoleCatalog/IRoleService.cs` (novo — catálogo de roles)
- `src/Identity.Infrastructure/RoleCatalog/RoleService.cs` (novo)
- `src/Identity.Api/Endpoints/RolesController.cs` (novo — `GET /api/roles`,
  autenticado)
- `tests/Identity.Tests/RolesTests.cs` (novo — 401 sem token; 200 com as 6 roles)
- `tests/Identity.Tests/AccountManagementTests.cs` (fluxos de conta)
- `tests/Identity.Tests/Lab/IdentityApiFactory.cs` (+ overrides dos novos limits)
- `CONTRATO-IDENTIDADE.md` (v1.1: novos endpoints, expirações, fluxos)
- `docs/memoria/etapa-11-gestao-conta.md` (este)
- `docs/memoria/INDEX.md` (status final Etapa 11 + pendências)

## Comandos executados

- `dotnet ef migrations add AddPasswordResetCodes`
- `dotnet build Identity.slnx`
- `dotnet test tests/Identity.Tests/Identity.Tests.csproj`

## Pendências que dependem do usuário (Etapa 11)

- ~~**Estratégia de revogação de refresh tokens**~~ — **confirmada**: manter o
  "armazenamento em banco + revogação por valor (logout/logout-all)" **sem
  denylist de JWT em memória/Redis** (access token sobrevive até os 15 min;
  decisão do usuário).
- ~~**Lifespan do token de reset por e-mail**~~ — **decidido**: **30 min** via
  `DataProtectionTokenProviderOptions.TokenLifespan` (aplica-se também ao token
  de confirmação de e-mail, que compartilha o provider).
- **Cadastro de roles internas (Manager/Seller/Delivery/TenantAdmin)** — o
  catálogo `GET /api/roles` (autenticado, qualquer role) já existe
  (`IRoleService`/`RoleService` + `RolesController`, namespace `RoleCatalog`);
  NÃO existe endpoint de **criação** de roles/usuários internos — hoje só via
  banco/seed ou futuros endpoints de admin.
- **Teste end-to-end real** de e-mail/SMS/reset com as credenciais reais
  (Gmail + Twilio) — depende das credenciais da Etapa 10.
