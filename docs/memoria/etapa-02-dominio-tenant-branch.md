# Etapa 02 — Domínio de Tenant e Branch

## Objetivo

Modelar o domínio de Tenant e Branch (Filial) com DDD: agregados, entidades,
value objects validados na criação, mapeamento EF Core via Fluent API e a
primeira migration — incluindo o índice único global de CNPJ em nível de banco.

## O que foi feito

1. **Value objects com validação no construtor** (`Domain/ValueObjects`):
   - `Cnpj` — normaliza para 14 dígitos, rejeita sequência idêntica e valida os
     dígitos verificadores com os pesos oficiais.
   - `Cpf` — mesmo padrão (11 dígitos).
   - `Email` — normaliza para minúsculas, valida formato e tamanho (≤ 254).
   - `Address` — endereço de filial com CEP e UF validados.
   - `Contact` — telefone (10/11 dígitos), telefone secundário e e-mail.
   - Todos lançam `ArgumentException` na construção — **um valor inválido nunca
     existe em memória**. Contêm `FromValidated(...)` para hidratar a partir do
     banco sem revalidar.
2. **IDs fortemente tipados** (`Domain/Common`): `TenantId` e `BranchId`
   (`readonly record struct`), mais as bases `Entity<TId>` e `ValueObject`.
3. **Entidades** (`Domain/Entities`):
   - `Tenant` (aggregate root): `LegalName` (razão social), `TradeName` (nome
     fantasia), `Cnpj`, `Email`, `TenantStatus`, `CreatedAtUtc` e coleção de
     branches. Exposição via `IReadOnlyCollection<Branch>` com `AddBranch`,
     `RemoveBranch`, `Activate/Deactivate/Suspend`.
   - `Branch` — `TenantId` obrigatório, `Name`, `Address`, `Contact`.
   - `Tenant.AddBranch(...)` cria a branch já associada ao agregado.
4. **Enums** (`Domain/Enums`): `TenantStatus` (Active, Inactive, Suspended).
5. **EF Core** (`Infrastructure/Persistence`):
   - `IdentityDbContext` com `DbSet<Tenant>`/`DbSet<Branch>` e
     `ApplyConfigurationsFromAssembly`.
   - `TenantConfiguration` e `BranchConfiguration` (Fluent API) — value objects
     como colunas via `HasConversion`; `Address`/`Contact` como `OwnsOne`.
   - `DependencyInjection.AddInfrastructure` registra o `DbContext` com Npgsql.
6. **Primeira migration** `InitialTenantAndBranches`, aplicada no Postgres do
   docker-compose, validando as tabelas e índices no banco.

## Decisões técnicas e por quê

- **Agregado Tenant como raiz**: todo acesso a branches passa pelo Tenant,
  mantendo invariante de unicidade de CNPJ e o ciclo de vida do agregado.
- **CNPJ do tenant com índice único GLOBAL** (`IX_tenants_cnpj`, `UNIQUE`):
  exigência de negócio (não há tenant pai para escopar). Implementado via
  `HasIndex(x => x.Cnpj).IsUnique()`.
- **CPF/CNPJ e e-mail unidos POR TENANT** (Manager/Seller/Delivery/Client) ficam
  para a Etapa 04, quando houver `TenantId` nas demais entidades.
- **Value objects como `OwnsOne`/`HasConversion`**: imutabilidade e validação na
  entidade, enquanto o banco continua relacional (colunas próprias por campo).
- **Strongly-typed IDs** (`TenantId`/`BranchId`): evitam `Guid` soltos ("primitive
  obsession") e dão clareza nas assinaturas; armazenados como `uuid`.
- **`Entity<TId>` com construtor privado + fábrica `Create`**: garante que só
  instâncias válidas nasçam via fábrica; construtor `protected` fica para o EF.
- **Query filters**: mapeei apenas `IX_branches_tenant_id` agora. A decisão de
  aplicar **named query filters do EF Core 10** (`HasQueryFilter("Name", expr)`,
  `IgnoreQueryFilters([...])`) para combinar escopo por tenant + soft-delete é
  para a Etapa 04, quando houver `TenantId` nas demais tabelas.

## Arquivos criados/alterados

- `src/Identity.Domain/Common/{Entity,ValueObject,TenantId,BranchId}.cs`
- `src/Identity.Domain/ValueObjects/{Cnpj,Cpf,Email,Address,Contact}.cs`
- `src/Identity.Domain/Entities/{Tenant,Branch}.cs`
- `src/Identity.Domain/Enums/TenantStatus.cs`
- `src/Identity.Infrastructure/Persistence/{IdentityDbContext,
  IdentityDesignTimeDbContextFactory}.cs`
- `src/Identity.Infrastructure/Persistence/Configurations/{Tenant,Branch}
  Configuration.cs`
- `src/Identity.Infrastructure/DependencyInjection.cs`
- `src/Identity.Infrastructure/Persistence/Migrations/`
  `{20260808151153_InitialTenantAndBranches}.cs` + `.Designer.cs`
- `src/Identity.Api/Program.cs` (registra `AddInfrastructure`)
- `src/Identity.Api/Identity.Api.csproj` (Design para `dotnet ef`)

## Comandos executados

- `dotnet add ... Microsoft.EntityFrameworkCore` 10.0.10,
  `Microsoft.EntityFrameworkCore.Relational` 10.0.10 e
  `Microsoft.EntityFrameworkCore.Design` 10.0.10 (QR)
- `dotnet build Identity.slnx` → **0 avisos, 0 erros** (após fix de warnings
  CS8618 com `= null!` nos setters privados).
- `dotnet ef migrations add InitialTenantAndBranches`
- `dotnet ef database update` (contra o Postgres do compose)
- Validação manual: INSERT de tenant com CNPJ duplicado → `duplicate key` no
  `IX_tenants_cnpj`; SELECT com `JOIN` tenant↔branch confirmou o mapeamento;
  TRUNCATE final para deixar o banco limpo.

## Decisão a validar (prova de conceito em Etapa 04)

Data/hora de criação: `CreatedAtUtc` é gravada pelo banco (`default sql now()`),
mas a entidade também define `DateTime.UtcNow` no construtor. Manter como está
(valor da entidade vence se informado; banco como fallback) ou alinhar a ambos.

## Pendências / Próximos passos

- **Etapa 03/04**: Identity (usuários/roles), unicidade por tenant e índices
  compostos; Named Query Filters (EF10) para escopo por tenant + soft-delete.
- Revisar colocações de `Address`/`Contact`: hoje `OwnsOne` compõe colunas na
  mesma tabela; se endreço vazar para outras entidades, avaliar `ComplexType`.
- Avaliar `SoftDelete` (DeletionFilter) quando entidades de usuários existirem.