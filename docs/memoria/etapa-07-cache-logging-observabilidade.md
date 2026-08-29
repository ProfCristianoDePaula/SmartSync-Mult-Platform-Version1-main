# Etapa 07 — Camadas Transversais: Cache, Logging Estruturado, Health Checks, Rate Limiting e Métricas

## Objetivo

Adicionar as camadas de produção que faltavam ao microsserviço, todas agnósticas
de domínio e reaproveitáveis entre os serviços da plataforma:
**cache** (memória + distribuído), **logging estruturado com Correlation ID**,
**health checks**, **rate limiting** nos endpoints sensíveis de auth e
**métricas/telemetria** (incluindo as nativas do Identity).

## O que foi feito

### 1. Logging estruturado com Serilog + Correlation ID

- **Serilog** como sink único (substitui o `ILogger` de console):
  console com template customizado `[{Timestamp} {Level}] [{CorrelationId}] ...`
  + arquivo rolante diário `logs/identity-api-.log` (retenção 14 dias).
  Config radiativa (`ReadFrom.Configuration`) respeitando `Serilog:MinimumLevel`;
  level do EF Core reduzido para `Warning` (ruído).
- **`CorrelationIdMiddleware`** (`Identity.Api/Observability`):
  - lê o header `X-Correlation-ID` de entrada (aceita até 64 chars) ou gera GUID;
  - injeta em `LogContext` → **todos os logs da request carregam a propriedade**;
  - espelha no header `X-Correlation-ID` da resposta (propagação entre serviços);
  - guarda em `HttpContext.Items["CorrelationId"]` para uso em negócio.
- **Ordem de pipeline importa**: `UseMiddleware<CorrelationIdMiddleware>` DEVE
  vir ANTES de `UseSerilogRequestLogging`, senão o log de "request responded" sai
  sem o correlation id (o escopo do `LogContext` já encerrou).
- `UseSerilogRequestLogging()` loga automaticamente cada request (método, rota,
  status, duração) com o Correlation ID.

### 2. Cache: memória + Redis/Valkey

- **`CacheOptions`** (`Application/Observability`) com `Mode` (`memory` |
  `redis`) e `RedisConnectionString`.
- `IMemoryCache` sempre registrado (cache de processo, cenários simples).
- Redis **condicional**: quando `Cache:Mode = "redis"` e há connection string,
  registra `IDistributedCache` via `Microsoft.Extensions.Caching.StackExchangeRedis`.
  Em produção/estaging multi-instância, use Redis/Valkey; o JWKS hoje usa memória.
- **Serviço Valkey** (`docker-compose.yml`): imagem `valkey/valkey:8-alpine`
  (fork open source do Redis), porta 6379, política de memória LRU 64 MB,
  healthcheck com `valkey-cli ping`. A API `depends_on` o banco **e** o valkey.
- **Cache do JWKS** (`JwksController`): usa `IMemoryCache`
  (`entry.SlidingExpiration = 5h`) para não reler o PEM a cada `/api/auth/jwks`;
  o cache é invalidado naturalmente a cada reinício do processo.
  - Nota: no `Mode = redis` o endpoint JWKS continua usando memória (chave pública
    é por-processo); o Redis fica para dados compartilhados entre instâncias.

### 3. Health checks com o Postgres

- `AspNetCore.HealthChecks.NpgSql` registra um check de **readiness** real:
  `SELECT 1` no banco (timeout 5s), exposto em `GET /api/health` (anônimo).
- Detalhe: usa a MESMA connection string do EF (`ConnectionStrings:
  DefaultConnection`), não uma chave nova `IdentityDb` (isso quebraria o check).

### 4. Rate limiting nativo nos endpoints de auth

- `AddRateLimiter` com políticas por **IP** (particiona por `RemoteIpAddress`):
  - `auth-login` → `POST /api/auth/login`: **5 req/min** (força bruta / spray);
  - `auth-refresh` → `POST /api/auth/refresh-token`: **10 req/min**;
  - rejeição com `429` (`RejectionStatusCode`).
- Aplicadas via `[EnableRateLimiting("...")]` nos endpoints (não global) para não
  impactar o resto da API; `app.UseRateLimiter()` antes de auth.
- Valores configuráveis via `RateLimiting` no `appsettings` (lidos no csproj
  como fixture; o pipe está feliz).
- Também é o comportamento natural do Identity: o middleware responde com
  `429 Too Many Requests` e o log Serilog registra o correlation id.

### 5. Métricas com OpenTelemetry (Prometheus)

- Pipeline `OpenTelemetry.Extensions.Hosting` + `OpenTelemetry.Instrumentation.
  AspNetCore`.
- `AddAspNetCoreInstrumentation()` → métricas automaticamente;
- `AddMeter("Microsoft.AspNetCore.Identity")` → expõe as métricas **nativas do
  Identity no .NET 10** (ex.: `aspnetcore_identity_user_check_password_attempts_total`,
  `sign_in_*`, `user.create.duration` etc.);
- `AddPrometheusExporter()` + `UseOpenTelemetryPrometheusScrapingEndpoint()`
  expõem tudo em `GET /metrics` no formato Prometheus.
- ⚠️ **Dependência beta**: `OpenTelemetry.Exporter.Prometheus.AspNetCore` só tem
  `1.17.0-beta.1` no NuGet (exige `--prerelease`). Estável ainda não foi lançada
  no momento desta etapa — acompanhar.

## Testes manuais executados (Docker)

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `docker compose up -d --build` (inclui valkey) | ✅ 3 serviços up, valkey healthy |
| 2 | `GET /api/health` anônimo | ✅ 200 `Healthy` |
| 3 | `GET /metrics` | ✅ 200, métricas `aspnetcore_authentication_*` |
| 4 | `GET /api/auth/jwks` x2 (cache IMemoryCache) | ✅ 200/200, 1 chave |
| 5 | Login com senha errada x6 (rate limit) | ✅ 1ª–5ª `400/401`, 6ª **429** (limite 5/min) |
| 6 | Login válido SuperAdmin → JWT | ✅ 200, token 813 chars |
| 7 | Métrica do Identity após login | ✅ `user_check_password_attempts_total{sua...}=1` |
| 8 | Correlation ID: cliente manda `smoke-login-01` | ✅ log Serilog e response header com o mesmo valor |
| 9 | Request log do middleware → HTTP GET respondido | ✅ visível com `[CorrelationId]` |
| 10 | `dotnet build Identity.slnx` | ✅ 0 avisos, 0 erros |

> Notas dos testes: o body do `login` usa `Identifier` (não `email`) — enviar
> `{"Identifier":"...","Password":"..."}`; o 400 em algumas tentativas era shape
> do body, não falha da API.

## Arquivos criados/alterados

- `src/Identity.Api/Observability/CorrelationIdMiddleware.cs` (novo)
- `src/Identity.Application/Observability/CacheOptions.cs` (novo)
- `src/Identity.Api/Endpoints/JwksController.cs` (cache IMemoryCache)
- `src/Identity.Api/Endpoints/AuthController.cs` (`[EnableRateLimiting]`)
- `src/Identity.Api/Program.cs` (Serilog, correlation, health, métricas, rate limiter)
- `src/Identity.Api/Identity.Api.csproj` (novos pacotes, abaixo)
- `src/Identity.Api/appsettings.json` (seções `Cache`, `RateLimiting`, `Serilog`)
- `docker-compose.yml` (serviço `valkey` + env `Cache__*` da API)

## Pacotes NuGet adicionados

```
Serilog.AspNetCore                   10.0.0
Serilog.Sinks.Console                 6.1.1
Serilog.Sinks.File                    7.0.0
AspNetCore.HealthChecks.NpgSql        9.0.0
Microsoft.Extensions.Caching.StackExchangeRedis  10.0.10
OpenTelemetry.Extensions.Hosting       1.17.0
OpenTelemetry.Instrumentation.AspNetCore         1.17.0
OpenTelemetry.Exporter.Prometheus.AspNetCore     1.17.0-beta.1  (--prerelease)
```

## Decisões técnicas e por quê

- **Serilog + Correlation ID**: é o destaque do pedido de observabilidade —
  cada log carrega o ID para correlacionar uma request entre os microsserviços.
- **`IMemoryCache` + Redis condicional**: cachê simples não precisa de rede e
  funciona offline; distribui quando há várias réplicas/`Mode=redis`. Evita
  dependência obrigatória de Redis em dev (quem quiser memoria, desliga via env).
- **Rate limit nativo (middleware)**: nenhuma lib externa; política fixa por IP
  é o suficiente para proteger login/refresh de spray de senha. Por endpoint
  (e não global) para não penalizar o resto da API.
- **Métricas nativas do Identity**: aproveita telemetria já embutida no .NET 10
  em vez de instrumentar manualmente cada login; Prometheus é o padrão de mercado.
- **Health check no Postgres**: o service composed espera `postgres healthy`; a
  API expõe próprio `ready` para orquestradores (K8s) saberem quando virar tráfego.

## Comandos executados

- `dotnet add package Serilog.AspNetCore`
- `dotnet add package Serilog.Sinks.Console`
- `dotnet add package Serilog.Sinks.File`
- `dotnet add package AspNetCore.HealthChecks.NpgSql`
- `dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis`
- `dotnet add package OpenTelemetry.Extensions.Hosting`
- `dotnet add package OpenTelemetry.Instrumentation.AspNetCore`
- `dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore --prerelease`
- `dotnet build Identity.slnx` → 0 avisos, 0 erros
- `docker compose up -d --build` (tec. agora 3 serviços)

## Pendências / Próximos passos

- **Acompanhar** o release do `OpenTelemetry.Exporter.Prometheus.AspNetCore`
  estável (> beta) e atualizar a referência.
- Elencar/criar **dashboards** no Grafana ou uso do `/metrics` (ex.: rate limit
  hit rate, login errors, `user_check_password_attempts`).
- Definir **quanto tempo** o JWKS cache (hoje 5h) vs. rotação de chave automática.
- Adicionar trace (W3C) se a plataforma adotar Tracing em vez de apenas métricas.