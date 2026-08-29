# Etapa 04 — Regras de Unicidade do Domínio Multi-Tenant

## Objetivo

Implementar as regras de unicidade do domínio multi-tenant:
- Documento (CPF/CNPJ) e e-mail únicos **por tenant** para Manager, Seller,
  Delivery e Client.
- CNPJ e e-mail **globalmente únicos** para Tenant e TenantAdmin.
- Validação **antes de persistir** na camada de Application (não depender só
  da constraint do banco).
- Erros de negócio claros em pt-BR.

## Regras de negócio definidas

| Escopo | Documento (CPF/CNPJ) | E-mail |
|--------|----------------------|--------|
| Manager, Seller, Delivery, Client | Único **por tenant** `(tenant_id, document)` | Único **por tenant** `(tenant_id, email)` |
| Tenant (entidade) | Único **global** `cnpj` | Único **global** `email` |
| TenantAdmin / SuperAdmin (usuario global) | Único **global** (checado em toda a plataforma) | Único **global** |

> **Importante (PostgreSQL):** índice único com colunas `NULL` trata valores
> `NULL` como distintos. O índice composto `(tenant_id, email)`/`(tenant_id,
> document)` portanto protege apenas o escopo **por tenant**. A unicidade
> **global** (usuários com `tenant_id` nulo, ex.: SuperAdmin/TenantAdmin) é
> garantida pela **validação na camada Application** (`IUniquenessChecker` com
> `tenantId = null`). Banco + Application são defesa em profundidade.

## O que foi feito

### 1. Índices únicos (migration `AddUniquenessRules`)

- `tenants`:
  - `IX_tenants_cnpj` (UNIQUE) — já existia desde a Etapa 02.
  - `IX_tenants_email` (UNIQUE) — **novo** (e-mail global do tenant).
- `users`:
  - `IX_users_tenant_email` (UNIQUE) sobre `(tenant_id, email)`.
  - `IX_users_tenant_document` (UNIQUE) sobre `(tenant_id, document)`.

Configuração em `ApplicationUserConfiguration.cs` e `TenantConfiguration.cs`.

### 2. Validação na camada de Application (antes de persistir)

- `IUniquenessChecker` (`Identity.Application/Uniqueness`): contrato de
  consulta de duplicidade, implementado por `UniquenessChecker`
  (`Identity.Infrastructure/Persistence`) usando `IdentityDbContext` direto.
- `TenantUniquenessValidator`: regras globais do tenant (CNPJ + e-mail).
- `UserUniquenessValidator`: escopo por tenant quando `tenantId` é informado;
  escopo global quando nulo (TenantAdmin/SuperAdmin).
- `BusinessRuleViolationException` (`Identity.Domain/Common`): erro de negócio
  com `Code` para tratamento de API, mensagens claras em pt-BR.

### 3. IdentityErrorDescriber em pt-BR

- `PtBrIdentityErrorDescriber` (`Identity.Infrastructure/Auth`): sobrescreve as
  mensagens padrão em inglês do ASP.NET Core Identity (DuplicateEmail,
  PasswordTooShort, etc.).
- Registrado via `.AddErrorDescriber<PtBrIdentityErrorDescriber>()` no builder
  de Identity.
- `RequireUniqueEmail = false`: a unicidade de e-mail **não** é mais imposta
  globalmente pelo Identity (agora é por tenant via índice composto + validators).

### 4. Registros no DI

- `AddScoped<IUniquenessChecker, UniquenessChecker>()`
- `AddScoped<TenantUniquenessValidator>()`
- `AddScoped<UserUniquenessValidator>()`

## Decisões técnicas e por quê

- **Defesa em profundidade**: a constraint do banco é a última linha de defesa;
  a validação acontece antes na camada Application com mensagens claras. Isso
  evita `DbUpdateException` com texto genérico para o usuário.
- **Escopo por tenant vs. global**: o mesmo CPF/e-mail pode existir em tenants
  diferentes (cada tenant é um mercado separado). Contudo, usuários **globais**
  (TenantAdmin/SuperAdmin) não podem duplicar e-mail/documento na plataforma.
- **`Email` vs `NormalizedEmail`**: o índice usa a coluna `Email`; a checagem de
  duplicidade usa `NormalizedEmail` (case-insensitive), mantendo consistência
  com o ASP.NET Identity.
- **Documento normalizado**: o checker remove máscara (`Where(char.IsDigit)`)
  antes de comparar, pois CPF/CNPJ são persistidos apenas com dígitos.

## Testes manuais executados (casos de teste anotados)

> Testes formais serão escritos na **Etapa 09**. Abaixo os casos validados
> manualmente no Postgres do docker-compose:

| # | Cenário | Resultado esperado | Resultado obtido |
|---|---------|--------------------|------------------|
| 1 | Inserir 2 tenants com o mesmo CNPJ | Rejeitado (`IX_tenants_cnpj`) | ✅ bloqueado |
| 2 | Inserir 2 tenants com o mesmo e-mail | Rejeitado (`IX_tenants_email`) | ✅ bloqueado |
| 3 | Mesmo CPF em tenants **diferentes** | Permitido | ✅ permitido |
| 4 | Mesmo CPF no **mesmo** tenant | Rejeitado (`IX_users_tenant_document`) | ✅ bloqueado |
| 5 | Mesmo e-mail no **mesmo** tenant | Rejeitado (`IX_users_tenant_email`) | ✅ bloqueado |
| 6 | Validação Application: tenant CNPJ/e-mail livres | Sem exceção | ✅ |
| 7 | Validação Application: e-mail global do SuperAdmin já existe | `IsUserEmailTakenAsync(null, ...) = true` | ✅ |
| 8 | Validação Application: documento por tenant não existe | `IsUserDocumentTakenAsync(tenantId, ...) = false` | ✅ |

## Arquivos criados/alterados

- `src/Identity.Domain/Common/BusinessRuleViolationException.cs`
- `src/Identity.Application/Uniqueness/{IUniquenessChecker,
  TenantUniquenessValidator, UserUniquenessValidator}.cs`
- `src/Identity.Infrastructure/Persistence/UniquenessChecker.cs`
- `src/Identity.Infrastructure/Auth/PtBrIdentityErrorDescriber.cs`
- `src/Identity.Infrastructure/Persistence/Configurations/
  {ApplicationUser,Tenant}Configuration.cs` (índices)
- `src/Identity.Infrastructure/DependencyInjection.cs`
- `src/Identity.Infrastructure/Persistence/Migrations/
  {20260808160200_AddUniquenessRules}.cs` + `.Designer.cs`

## Comandos executados

- `dotnet build Identity.slnx` → 0 avisos, 0 erros
- `dotnet ef migrations add AddUniquenessRules` (local, Windows)
- `docker compose up -d --build` (aplica migrations no bootstrap)
- Validação SQL no container: testes 1–5 da tabela acima
- Smoke test do `UniquenessChecker` (console temporário): testes 6–8

## Pendências / Próximos passos

- **Etapa 05**: login social (Google/Facebook) exclusivo para a role Client.
- Endpoints de CRUD de tenant/usuário ainda não existem; os validators de
  unicidade devem ser chamados nesses fluxos quando forem criados.
- Revisar se `RequireUniqueEmail=false` impacta fluxos que hoje assumem e-mail
  globalmente único (ex.: `FindByEmailAsync` em login local).
