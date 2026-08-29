# Etapa 01 — Estrutura da Solução e Docker

## Objetivo

Criar o esqueleto da solução .NET 10 em Clean Architecture + DDD e o ambiente
Docker básico (PostgreSQL + API).

## O que foi feito

1. **Solução** criada no formato **`.slnx`** (`Identity.slnx`) com 4 projetos:
   - `Identity.Domain` (classlib, sem dependências)
   - `Identity.Application` (classlib, referencia Domain)
   - `Identity.Infrastructure` (classlib, referencia Application — herdando Domain)
   - `Identity.Api` (webapi, referencia Application e Infrastructure)
2. **Referências** configuradas respeitando Clean Architecture (regra de
   dependência apontando para dentro; Api não referencia Domain diretamente).
3. **Pacotes iniciais** (Infrastructure):
   - `Npgsql.EntityFrameworkCore.PostgreSQL` **10.0.3**
   - `Microsoft.EntityFrameworkCore.Design` **10.0.10** (para migrations futuras)
   - `Microsoft.OpenApi` **2.11.0** (fix de vulnerabilidade GHSA-v5pm-xwqc-g5wc)
4. **Dockerfile multi-stage** (sdk → publish → aspnet, expõe porta 8080).
5. **docker-compose.yml** com serviços `api` e `postgres` (imagem `postgres:16-alpine`,
   volume nomeado `pgdata`, variáveis de ambiente, healthcheck `pg_isready`).
6. **appsettings.Development.json** com `ConnectionStrings:DefaultConnection`
   (sem senha commitada — só usuário/servidor/banco).
7. **user-secrets** da `Identity.Api` com a connection string completa de dev.
8. **Esqueleto de pastas DDD** (Domain: Entities/ValueObjects/Enums/Common;
   Application: Interfaces/Features/Common; Infrastructure: Persistence/Extensions)
   com `.gitkeep`.
9. **Program.cs** limpo: apenas `/api/health` e OpenAPI em Development.

## Decisões técnicas e por quê

- **`.slnx` em vez de `.sln`**: formato XML novo, mais enxuto e legível em diffs;
  nativo do .NET 10; escolhido em consenso com o usuário.
- **Clean Architecture nas referências**: Domain sem pacotes externos; Application
  depende apenas de Domain; Infrastructure implementa as interfaces; Api orquestra.
  Isso mantém o núcleo independente de EF, Npgsql e da web.
- **EF Core + Npgsql apenas na Infrastructure**: segue a arquitetura; sem modelagem
  de entidades ainda (próxima etapa).
- **`Microsoft.OpenApi` 2.11.0 explícito na Api**: a dependência transitiva
  `2.0.0` disparava warning NU1903 (vulnerabilidade); pinamos a versão 2.x
  compatível com `Microsoft.AspNetCore.OpenApi` 10 (a 3.x quebra o source generator).
- **Credenciais `postgres/postgres`, banco `identity`**: definidas pelo usuário.
- **Senha nunca em appsettings commitado**: usa-se `dotnet user-secrets` em dev
  e variável de ambiente `ConnectionStrings__DefaultConnection` no compose (que
  aponta para o host `postgres` da rede interna).

## Arquivos criados/alterados

- `Identity.slnx`
- `Dockerfile`
- `docker-compose.yml`
- `.gitignore`, `.dockerignore`
- `src/Identity.Domain/` (csproj + pastas DDD)
- `src/Identity.Application/` (csproj + pastas)
- `src/Identity.Infrastructure/` (csproj + pastas + pacotes)
- `src/Identity.Api/` (csproj, Program.cs, appsettings*.json, user-secrets)

## Comandos executados

- `dotnet new sln --name Identity --format slnx`
- `dotnet new classlib/webapi` (4 projetos)
- `dotnet sln Identity.slnx add ...` (4 csproj)
- `dotnet add reference` (Application→Domain, Infrastructure→Application,
  Api→Application e Infrastructure)
- `dotnet add package` (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3,
  Microsoft.EntityFrameworkCore.Design 10.0.10, Microsoft.OpenApi 2.11.0)
- `dotnet user-secrets init/set ... --project src/Identity.Api`
- `dotnet build Identity.slnx` → **0 avisos, 0 erros**

## Pendências / Próximos passos

- **Docker validado**: containers `identity_api` (Up) e `identity_postgres`
  (healthy) rodando com `docker compose up -d --build`. `GET /api/health`
  responde `200 {"status":"ok"}`. Postgres confirmou `SELECT current_database()`
  → `identity`, usuário `postgres`. Imagem: `identity-tenants-api:latest`.
- Modelar entidades (Tenant, Branch, usuários/roles) e configurar Identity +
  DbContext + primeira migration (Etapa 02+).
- Confirmar estratégia de multi-tenancy (claim/header, schema por tenant, etc.).
- Para derrubar o ambiente: `docker compose down` (ou `down -v` p/ apagar volume).
