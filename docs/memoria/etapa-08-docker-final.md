# Etapa 08 — Containerização Completa do Microsserviço

## Objetivo

Consolidar a containerização definitiva: **Dockerfile multi-stage** enxuto e
seguro (non-root), **docker-compose** com rede dedicada e variáveis via `.env`,
**override de dev** separado da produção e **README** documentando o fluxo
de subida/migrations/verificação local.

## O que foi feito

### 1. Dockerfile multi-stage revisado

- **Stage `build`** (SDK `dotnet/sdk:10.0`): copia apenas os `.csproj` +
  `Identity.slnx` antes de `dotnet restore` → **cache de camadas eficiente**
  (restore só roda quando dependências mudam). Depois copia `src/` e faz
  `dotnet publish -c Release -o /app/publish --no-restore`.
- **Stage `final`** (runtime `aspnet:10.0`, enxuto, sem SDK):
  - instala apenas `curl` (healthcheck do container);
  - cria `/app/keys` e `/app/logs` com permissão do **usuário não-root** `app`
    (UID 1654, padrão das imagens .NET 10) e roda com `USER $APP_UID`;
  - `ENV ASPNETCORE_URLS=http://+:8080` e `EXPOSE 8080`;
  - entrypoint `dotnet Identity.Api.dll`.

### 2. docker-compose.yml (base = produção)

- **Rede dedicada `identity-net`** (bridge) — todos os serviços nela.
- **Variáveis via `.env`** com defaults seguros: `${VAR:-default}`; o então
  obrigatório `SUPERADMIN_PASSWORD:?» não pode faltar no `.env`.
- **Portas de infraestrutura não expostas em produção** — Postgres (5432) e
  Valkey (6379) só têm `ports` no override de dev (rede interna pura).
- **`depends_on` com healthcheck para TODOS os serviços**:
  - API depende de `postgres` (`service_healthy`) e `valkey` (`service_healthy`);
  - healthchecks próprios: `pg_isready`, `valkey-cli ping`, e da API
    `curl …/api/health`.
- API com `healthcheck`, `Jwt__SigningKeyPath=/app/keys/...`, volume `jwtkeys`.

### 3. docker-compose.override.yml (dev)

- Aplicado automaticamente pelo `docker compose up`.
- Expõe `5432`/`6379` para a máquina host (psql/cliente Valkey locais).
- Força `ASPNETCORE_ENVIRONMENT=Development` (MapOpenApi/OpenAPI ligado).
- **Nenhum segredo** — 100% leitura do `.env`.
- Para produção: `docker compose -f docker-compose.yml up -d`.

### 4. Segredos fora do repositório

- `.env` real **ignorado no git** (`.gitignore` ganhou `.env`).
- `.env.example` criado com as MESMAS chaves — documentado para `cp` e edição.
- `.gitignore` também ignora `logs/` (sink de arquivo do Serilog).

### 5. README.md (novo) — fluxo completo documentado

Subir tudo: `cp .env.example .env` → `docker compose up -d --build` → checar
`curl /api/health` e `/metrics`. **Migrations aplicadas automaticamente no
startup** (seeder); opção de `dotnet ef database update` manual documentada.
Inclui: tabela de portas, comandos úteis, modo produção (`-f docker-compose.yml`),
dev sem Docker (user-secrets + `up -d postgres valkey`), estrutura do repo,
segurança (non-root, sem segredos commitados, JWT no volume).

## Testes manuais executados (Docker)

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `docker compose config --quiet` | ✅ válido |
| 2 | `docker compose up -d --build` | ✅ 3 serviços up; dependências por healthcheck |
| 3 | `docker compose ps` | ✅ `api`, `postgres`, `valkey` todos **healthy** |
| 4 | Container da API roda como `app` (UID 1654) | ✅ `id -un`/`id -u` → app/1654 |
| 5 | `GET /api/health` | ✅ 200 `Healthy` |
| 6 | `GET /api/auth/jwks` | ✅ 200 `{"keys":[...]}` (kid `identity-signing-key`) |
| 7 | `GET /metrics` | ✅ 200 (Prometheus) |
| 8 | Volume `/app/logs` gravado pelo non-root | ✅ `identity-api-20260808.log` owner `app` |
| 9 | Login SuperAdmin | ✅ 200, access token 813 chars |
| 10 | Imagem enxuta (curl p/ healthcheck, sem SDK no runtime) | ✅ runtime ~ aspnet base |

>  **Nota de deploy limpo**: o `.pem` atual no volume `jwtkeys` ficou owner
>  `root` por vir de volume criado em etapas antigas. Após `docker compose down
>  -v` (apagar volume), o `/app/keys` nascerá owner `app` graças ao `chown` do
>  Dockerfile — comportamento corrigido para deploy novo.

## Arquivos criados/alterados

- `Dockerfile` (multi-stage revisado: cache de restore, non-root, curl)
- `docker-compose.yml` (rede `identity-net`, pult/env-defaults, healthchecks)
- `docker-compose.override.yml` (novo)
- `.env.example` (novo)
- `.env` (mantido, agora com chaves explícitas do compose)
- `.gitignore` (+ `.env`, + `logs/`)
- `README.md` (novo)

## Decisões técnicas e por quê

- **Restore antes de copiar o código**: camadas Docker ficam cacheadas — rebuild
  só refaz `dotnet restore` se os `.csproj` mudarem (velocidade em CI/dev).
- **`--no-restore` no publish**: não duplica o restore caro dentro do container.
- **Non-root `app`**: boa prática de segurança de container — mitigação caso o
  processo seja comprometido (não roda com root no filesystem).
- **Segredos via `.env` + `.env.example`**: mantém senhas fora do git; exemplo
  versionado documenta os nomes das variáveis.
- **rede `identity-net` dedicada**: isola o sistema do driver default e facilita
  futura comunicação com outros microsserviços da plataforma na mesma rede.
- **Ports PostgreSQL/Valkey locked no override dev**: produção não expõe infra
  interna; dev expõe para tooling local sem custo.
- **Healthcheck no compose, não no Dockerfile**: Dockerfile roda para todos os
  runtimes; o compose orquestra dependência/ordem (e é onde faz sentido declarar).

## Comandos executados

- `docker compose config --quiet`
- `docker compose up -d --build`
- `docker compose ps`
- `docker exec identity_api id -un` / `docker exec identity_api sh -c "ls -l /app/keys /app/logs && whoami"`
- Verificação endpoints: `/api/health`, `/api/auth/jwks`, `/metrics`, login

## Pendências / Próximos passos

- Definir **CI/CD** (ex.: GitHub Actions: `docker buildx`/compose up + smoke
  test + push para registry) — etapa futura.
- Adicionar **secrets manager** (Docker Secrets / cloud secret store) para
  produção, além do `.env` local.
- Considerar **tracing W3C** e dashboards Grafana (já anotado na Etapa 07).
</content>