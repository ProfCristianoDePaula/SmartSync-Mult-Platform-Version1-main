# Etapa 36 — Fiscal-1: Esqueleto do microsserviço Fiscal

> Mapeamento do roteiro: Fiscal-1 = Etapa 36. Data-base 19/09/2026.

## Objetivo

Criar o microsserviço Fiscal vazio, seguro e integrado ao Identity, no padrão do Estoque (ADR-001 D-ADR1).

## Entregas

- `Fiscal.slnx` + `src/Fiscal.Domain` + `src/Fiscal.Application` + `src/Fiscal.Infrastructure` + `src/Fiscal.Api` + `tests/Fiscal.Tests` (net10.0; mesmas versões do Estoque: EF Core 10.0.10, Npgsql 10.0.3, JwtBearer 10.0.10, FluentValidation 12.1.1, Scalar 2.16.18, Serilog 10.0.0, OTel/Prometheus, HealthChecks Npgsql 9.0.0, Testcontainers 4.9.0).
- `Dockerfile.fiscal` (multi-stage, não-root `$APP_UID`, curl p/ healthcheck) + compose `fiscal-postgres` + `fiscal-api` (porta host `8082`, rede `identity-net`, depends saudáveis, `Fiscal__ProducaoHabilitada=false` por padrão — R10) + `.env.example` (`FISCAL_API_PORT/POSTGRES_*`, `FISCAL_PRODUCAO_HABILITADA=false`) + override dev (porta `5434` do Postgres, `Development`, `Identity__BaseUrl`).
- Auth: JWT via JWKS remoto (`JwksKeyStore` + `JwksRefreshService`, `MapInboundClaims=false`, Issuer/Audience por env) + policies `tenant` (exige claim, SuperAdmin 403 — R5) e `module-fiscal` (slug `fiscal`, fail-closed via `GET /api/tenants/me/modules` com cache 5 min).
- `IExceptionHandler` global → ProblemDetails pt-BR (`BusinessRuleViolationException`→400, `UnauthorizedAccessException`→403, demais→500 sem vazar detalhes — R4/R13) + Serilog + `CorrelationIdMiddleware` + `/api/health` + Scalar em `/scalar` (dev) + `/metrics`.
- `audit_log` (`id, tenant_id?, user_id?, action/entity 200, entity_id? 200, occurred_at_utc` + índices `(tenant,ocorrido)` e `(tenant,entidade)`, sem soft delete — R9) + `IAuditLogger` (só metadados, nunca segredos — R4) + migration `20260919162855_InitialCreate` gerada via container SDK (mesmo padrão do Estoque) + `FiscalDbInitializer.MigrateAsync` no arranque + design-time factory.
- `GET /api/fiscal/ping` (`module-fiscal`): prova o gate e exercita o audit de ponta a ponta. Sem regra fiscal — emissão real começa na Fiscal-8.
- `docs/fiscal/CADASTRO-DO-MODULO.md`: cadastro de `fiscal` + plano + vínculo pelo fluxo oficial (SuperAdmin). **Zero mudanças no Identity** (ADR não aprovou seed; R2).
- Testes Testcontainers Postgres reais (R12, sem mock de banco): health 200; sem token 401; SuperAdmin sem tenant 403 (R5); sem módulo 403; com módulo 200 + linha de auditoria; tenant B sem acesso à trilha do A (R5). Script `scripts/run-tests-in-docker.fiscal.ps1`.
- `src/Fiscal.Infrastructure/Persistence/ValueConverters.cs` central (Tenant/Branch/AuditLog, nullable-safe) — correção de compilação CS1503/CS8629 no mapeamento de `TenantId?`.

## Decisões

- Espelhamento fiel do Estoque (R13), sem rate limiter próprio ainda (só existia p/ upload XML; hardening é Fiscal-14).
- `GlobalExceptionHandler` em vez de só helpers de controller: cobre `GetRequiredTenantId()` (403 em vez de 500) desde o esqueleto.
- Porta `8082` livre confirmada (Identity `8080`, Estoque `18081` neste repo); `FISCAL_POSTGRES_PORT=5434`.
- Unimake.DFe **não** referenciada ainda (spike de compilação net10 é condição da Fiscal-9, não desta etapa — R15).

## Validação

- `dotnet build Fiscal.slnx` — 0 erros; só avisos NU1903 `SSH.NET` transitivos do Testcontainers (mesmos do Identity/Estoque, preexistentes no ecossistema).
- `scripts/run-tests-in-docker.fiscal.ps1` — **6/6 Passed** (1 falha intermediária de tradução LINQ `TenantId.Value.Value` corrigida para igualdade de struct, padrão Estoque).
- `docker compose up -d --build fiscal-api` — 7/7 containers healthy; `GET :8082/api/health` → Healthy; `GET :8082/api/fiscal/ping` sem token → 401 com `X-Correlation-ID`.

## Arquivos principais

`Fiscal.slnx`, `src/Fiscal.*/**`, `tests/Fiscal.Tests/*`, `Dockerfile.fiscal`, `docker-compose.yml`, `docker-compose.override.yml`, `.env.example`, `scripts/run-tests-in-docker.fiscal.ps1`, `docs/fiscal/CADASTRO-DO-MODULO.md`.

## Pendências

- Cadastrar `fiscal` + plano + vínculo via `CADASTRO-DO-MODULO.md` (manual, com SuperAdmin) antes dos testes manuais com token real.
- `PERGUNTAS.md` 1–4 seguem abertas (condicionam Fiscal-4+).
- `PENDENCIAS.md` F0-01–F0-11 mantidas (`verificado=false`).
- Próxima: Fiscal-2 (Etapa 37) — UFs/autorizadores/endpoints SEFAZ, só com fonte oficial.
