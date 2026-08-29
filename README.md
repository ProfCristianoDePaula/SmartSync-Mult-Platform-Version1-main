# Identity & Tenants — Microserviço de Identidade

Microsserviço de **autenticação, autorização e cadastro de tenants/filiais** da
plataforma, construído em **.NET 10** com Clean Architecture + DDD.

| Componente | Tecnologia |
|---|---|
| API | ASP.NET Core 10 (`Identity.Api`) |
| Banco | PostgreSQL 16 (`identity_postgres`) |
| Cache distribuído | Valkey (Redis open source, `identity_valkey`) |
| Autenticação | JWT RS256 (JWKS) + OAuth Google/Facebook + Identity |
| Observabilidade | Serilog (Correlation ID) + OpenTelemetry/Prometheus (`/metrics`) |

---

## Pré-requisitos

- [Docker](https://www.docker.com/products/docker-desktop/) com **Docker Compose v2**
  (já incluso no Docker Desktop)
- `git`

Não é necessário ter o SDK do .NET instalado para subir o stack completo — tudo
roda em containers.

---

## Subir tudo localmente (Docker)

### 1. Preparar o ambiente

```bash
cp .env.example .env
# edite o .env: defina uma senha forte para SEED_SUPERADMIN_PASSWORD
```

> O `.env` não é versionado (contém segredos). Os defaults do
> `docker-compose.yml` já funcionam sem ele, mas o `SEED_SUPERADMIN_PASSWORD` é
> obrigatório quando o compose define o seed.

### 2. Build e subida

```bash
docker compose up -d --build
```

O compose **dev** (`docker-compose.override.yml` é aplicado automaticamente)
sobe **Postgres, Valkey e a API**, expondo as portas na sua máquina:

| Serviço | Container | Porta host |
|---|---|---|
| API | `identity_api` | `http://localhost:8080` |
| Postgres | `identity_postgres` | `localhost:5432` |
| Valkey | `identity_valkey` | `localhost:6379` |

O `depends_on` com healthcheck garante a ordem: a API só inicia quando
Postgres **e** Valkey estiverem saudáveis. A própria API tem healthcheck
(`curl /api/health`).

### 3. Migrations e seed

**As migrations são aplicadas automaticamente** na inicialização
(`IdentitySeeder.SeedAsync` → `dbContext.Database.MigrateAsync()`), junto com o
seed das roles, dos dados da plataforma e do SuperAdmin
(`DbSeeder.SeedAsync` — ver `docs/SEED-INICIAL.md`). Não é preciso rodar
comandos extras.

Caso queira aplicar migrations manualmente (ex.: em CI/CD), dentro da API:

```bash
docker compose exec api dotnet ef database update \
  --no-build --project src/Identity.Infrastructure \
  --startup-project src/Identity.Api \
  --connection "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres"
```

> O caminho dos projetos é relativo ao container; ajuste ao seu fluxo real de CI.

### 4. Verificar

```bash
docker compose ps                 # todos "healthy"/"running"
curl http://localhost:8080/api/health   # → Healthy
curl http://localhost:8080/metrics      # métricas Prometheus
curl http://localhost:8080/api/auth/jwks # chave pública (JWKS)
```

Login de exemplo (após o seed):

```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"identifier":"sa@smartsync.com.br","password":"SUA_SENHA_DO_ENV"}'
```

---

## Modo produção (sem sobreposição de dev)

O `docker-compose.override.yml` **não** é aplicado quando você informa
explicitamente o arquivo base — útil em CI/CD ou servidor:

```bash
docker compose -f docker-compose.yml up -d --build
```

Nesse modo, Postgres e Valkey **não** expõem portas no host (rede interna
`identity-net`), e a API roda com `ASPNETCORE_ENVIRONMENT=Production`.

---

## Comandos úteis

```bash
docker compose logs -f api           # logs da API (com Correlation ID)
docker compose logs -f postgres      # logs do banco
docker compose ps                    # status dos serviços
docker compose restart api           # reiniciar só a API
docker compose down                  # derrubar (mantém volumes)
docker compose down -v               # derrubar APAGANDO volumes (dados perdidos)
docker compose up -d --build api     # rebuild só da API
```

Para inspecionar o banco direto da sua máquina (porta exposta só em dev):

```bash
docker exec -it identity_postgres psql -U postgres -d identity
```

---

## Estrutura do repositório

```
├── Dockerfile                      # multi-stage (build → runtime não-root)
├── docker-compose.yml              # stack base (prod)
├── docker-compose.override.yml     # sobreposição de dev (portas + Development)
├── .env.example                    # modelo de variáveis (SEM segredos)
├── Identity.slnx                   # solução .NET 10
├── src/
│   ├── Identity.Domain/            # entidades, VOs, enums (sem dependências)
│   ├── Identity.Application/       # casos de uso / interfaces
│   ├── Identity.Infrastructure/    # EF Core, Npgsql, Identity, senders
│   └── Identity.Api/               # endpoints REST, middleware, Program.cs
└── docs/memoria/                   # memórias por etapa (decisões e contexto)
```

> **Memórias de etapa:** antes de qualquer trabalho, consulte
> [`docs/memoria/INDEX.md`](docs/memoria/INDEX.md) — é o registro cumulativo de
> decisões e pendências do projeto.

---

## Documentação de apoio

- [docs/FLUXO-DE-CADASTRO.md](docs/FLUXO-DE-CADASTRO.md) — **ordem oficial de
  cadastro** das entidades (Module → Plan por Module → Tenant → vínculo
  Tenant-Module → Branch → Usuários), com exemplos e quem executa cada passo.
- [docs/CONTRATO-IDENTIDADE.md](docs/CONTRATO-IDENTIDADE.md) — contrato oficial
  da API (rotas, roles, payloads, modelo de dados e exemplos `curl`).
- [docs/EXECUCAO-PARA-TESTES.md](docs/EXECUCAO-PARA-TESTES.md) — **como iniciar a
  API para testes** (Development local ou Docker Compose) e a bateria de teste de
  todos os endpoints de ponta a ponta.
- [docs/CONFIGURACAO-CREDENCIAIS.md](docs/CONFIGURACAO-CREDENCIAIS.md) — guia de
  credenciais externas (Google, Facebook, Twilio, Gmail).
- [docs/memoria/](docs/memoria/) — memórias de desenvolvimento por etapa
  (decisões e histórico; comece pelo `INDEX.md`).

---

## Ambiente de desenvolvimento (sem Docker)

Rode Postgres/Valkey via Docker e a API via `dotnet run` (hot reload):

```bash
# terminal 1 — infraestrutura (dev)
docker compose up -d postgres valkey

# terminal 2 — API com hot reload
cd src/Identity.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=identity;Username=postgres;Password=postgres"
dotnet run
```

A API sobe em `http://localhost:5000` (ou a porta do launchSettings); em
Development a UI de testes (Scalar) fica em `/scalar` e a spec em
`/openapi/v1.json`.

---

## Segurança

- **Nenhum segredo no repositório**: credenciais apenas via `.env`
  (ignorado pelo git) ou `user-secrets`/variáveis de ambiente.
- Chaves RSA de assinatura JWT ficam no volume `jwtkeys` (`/app/keys`), nunca
  commitadas.
- O container da API roda como **usuário não-root** (`app`, UID 1654).
- Rate limiting por IP nos endpoints de login/refresh (5/min e 10/min).
