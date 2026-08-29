# Etapa 03 — Identity, Roles e JWT

## Objetivo

Configurar o ASP.NET Core Identity com usuário customizado e as roles da
plataforma, e a emissão de JWT — este serviço é o **provedor de identidade**
para os demais microsserviços.

> **Nota de cronologia:** a implementação desta etapa foi feita em sessão
> anterior à da Etapa 04, mas **não foi registrada** na memória na época.
> Este arquivo documenta retroativamente o que já está implementado e
> validado no código.

## O que foi feito

### 1. `ApplicationUser : IdentityUser<Guid>` (usuário customizado)

- `Identity.Infrastructure/Persistence/Identity/ApplicationUser.cs`.
- Herda de `IdentityUser<Guid>` (traz email, telefone, bloqueio, etc.).
- Campos customizados:
  - `TenantId?` (`TenantId` do Domain) — **nullable**: SuperAdmin/TenantAdmin
    globais não pertencem a um tenant; Manager/Seller/Delivery/Client sim.
  - `FullName` — nome completo do usuário.
  - `Document` — CPF (pessoa física) ou CNPJ (empresa) conforme a role.
  - `CreatedAtUtc`.
- Role custom: `ApplicationRole : IdentityRole<Guid>`.
- Migration `AddIdentity` cria as tabelas do Identity (`users`, `AspNetRoles`,
  `AspNetUserRoles`, `AspNetRoleClaims`, `AspNetUserClaims`, `AspNetUserLogins`,
  `AspNetUserTokens`).

### 2. Roles da plataforma (seed)

- `Roles` (`Domain/Common/Roles.cs`): constantes `SuperAdmin`, `TenantAdmin`,
  `Manager`, `Seller`, `Delivery`, `Client` + lista `Roles.All`.
- `IdentitySeeder` (`Infrastructure/Auth/IdentitySeeder.cs`):
  - aplica migrations pendentes no bootstrap;
  - cria as 6 roles se não existirem;
  - cria o **SuperAdmin de bootstrap** somente se credenciais vierem da
    configuração (`SeedSuperAdmin:Email`/`:Password`) — nunca hardcode.
  - No docker-compose a senha vem de `.env` (`SUPERADMIN_PASSWORD`).

### 3. Autenticação JWT (emissão própria + JWKS)

Sem servidor OIDC completo (decisão da etapa 00): troca por chave assimétrica
RSA própria + endpoint JWKS para os outros microsserviços validarem.

- `SigningKeyProvider` (`Infrastructure/Security`):
  - RSA 2048 gerada/persistida em **PEM** (`keys/jwt-signing-key.pem`,
    fora do repositório; diretório `keys/` no `.gitignore`).
  - singleton compartilhado entre emissão e validação.
  - expõe `SigningCredentials` (RS256) e `PublicKey` (JWK).
- `JwtOptions` (`Application/Auth`): Issuer, Audience, lifetimes, key path, KeyId
  (seção `Jwt` no appsettings; sobrescrita por env no compose).
- `TokenService`:
  - access token JWT RS256 com claims:
    - `user_id`, `full_name`, `email`;
    - `tenant_id` (quando o usuário pertence a um tenant);
    - `role` (array de roles).
  - refresh token com **rotação**: cada uso gera um novo e revoga o anterior
    (hash SHA-256 gravado na tabela `refresh_tokens`, com datas de expiração/
    revogação/replace).
- Validação no `Program.cs` via `Microsoft.AspNetCore.Authentication.JwtBearer`
  usando `IssuerSigningKeyResolver` com a MESMA chave do emissor singleton.
- `/api/auth/jwks` · `JWKS` expõe a chave pública para os validadores externos.

### 4. Endpoints

- `POST /api/auth/login` — identifier (email ou nome) + senha → `TokenResponse`
  (access token, expiração em segundos, refresh token + expiração).
- `POST /api/auth/refresh-token` — troca refresh token por novo (rotação);
  reutilização do token antigo é rejeitada (401).
- `GET /api/auth/jwks` — conjunto JWKS da chave pública de assinatura.

## Testes manuais executados (sessão anterior, via Docker)

| Cenário | Resultado |
|---------|-----------|
| Login do SuperAdmin bootstrapped (`/api/auth/login`) | ✅ access token JWT RS256 com `kid=identity-signing-key` e claims corretas |
| Refresh token (`/api/auth/refresh-token`) | ✅ novo refresh emitido |
| Reuso do refresh token antigo | ✅ 401 (rotação inválida o anterior) |
| `.wise` (`/api/auth/jwks`) | ✅ `{ "keys": [ { kid, kty: RSA, n, e, alg: RS256, use: sig } ] }` |

## Decisões técnicas e por que

- **Chave RSA própria + JWKS em vez de servidor OIDC completo**: atende aos
  microsserviços validando tokens com a mesma chave pública, sem a complexidade
  operacional de um OIDC completo (OpenIddict fica como alternativa se o
  requisito de SSO/SRW crescer).
- **Refresh token rotativo com hash (SHA-256)**: nunca guardamos o valor em
  claro no banco; detectamos reuso e mitigamos roubo.
- **Roles no JWT como array (`role`)**: multi-role por usuário viabilizada
  (ex.: Client pode vir a ter mais de uma role).

## Arquivos criados/alterados

- `src/Identity.Infrastructure/Persistence/Identity/{ApplicationUser,
  ApplicationRole, RefreshToken}.cs`
- `src/Identity.Infrastructure/Auth/{AuthService,EntitySeeder}.cs`
- `src/Identity.Infrastructure/Security/{SigningKeyProvider,TokenService}.cs`
- `src/Identity.Application/Auth/{IAuthService,JwtOptions,LoginRequest,
  RefreshRequest,TokenResponse}.cs`
- `src/Identity.Domain/Common/{Roles,JwtClaims}.cs`
- `src/Identity.Api/Endpoints/{AuthController,JwksController}.cs`
- `src/Identity.Api/Program.cs` (+ JwtBearer)
- Migration `AddIdentity` (+ `.Designer`).
- `appsettings.json` (seção `Jwt`) + `docker-compose.yml` (envs + volume de key)
- `.gitignore` (`keys/`, `.env`)

## Comandos executados

- `dotnet ef migrations add AddIdentity` (gerada nos Windows)
- `dotnet build Identity.slnx` → 0 avisos, 0 erros
- `docker compose up -d --build` (auto-migra + seed)
- Chamadas de API para login/refresh/jwks (tabela acima)

## Pendências / Decisão de documentação

- **`RequireConfirmedEmail = true` no login local**: por enquanto não há fluxo
  de confirmação de e-mail implementado (enviar e-mail é etapa futura). O
  SuperAdmin é confirmado no seed. Validar impacto disso em usuários Client
  criados daqui pra frente.
- **Confirmação/recuperação de senha** e **validação de e-mail (SMS)** seguem
  como próximas etapas.
- **Passkeys/WebAuthn**: o ASP.NET Core Identity no .NET 10 ganhou suporte a
  passkeys (WebAuthn) — **não implementado**; registrado no INDEX como possível
  evolução futura.