# Etapa 24 — Implementação do Microsserviço de Estoque

**Data:** 22/08/2026
**Status:** Feita (final) — build limpo, suíte de integração 3/3 no container

## Objetivo

Implementar o design aprovado na Etapa 23: solução completa do microsserviço
de Estoque (Clean Architecture espelhando o Identity), integração JWT/JWKS,
multi-tenant/multi-filial, outbox e jobs.

## O que foi entregue

### Solução `Estoque.slnx` (5 projetos)

- `src/Estoque.Domain` — bases (`Entity<TId>`, `ValueObject`,
  `IHasDomainEvents`, `BusinessRuleViolationException`), **12 IDs tipados**
  (`ProductId`…`AlertId`) + `TenantId`/`BranchId` lógicos, cópia-contrato de
  `JwtClaims`/`PlatformRoles`, VOs (`Sku`, `Barcode` EAN-13, `Quantity` c/
  operadores, `Money`, `Cpf`, `Cnpj`, `Documento`, `Email`, `Address`,
  `Contact`), enums (`MovementType`, `AlertType/Severity`,
  `PurchaseSuggestionStatus`, `XmlImportStatus`, `OutletReason`,
  `UnitOfMeasure`+fatores, `TipoPessoa`), eventos (`ProductCreated`,
  `StockMovementRegistered`, `LowStockDetected`, `LotNearExpiry`,
  `OutletMarked`, `PurchaseSuggestionCreated`…), **13 entidades** com
  invariantes e **serviços de domínio puros**: `MovementApplier`
  (in/out/ajuste/**transferência par espelhado atômico**, custo médio
  ponderado) e `ReplenishmentPolicy` (ruptura/excesso/sugestão + janela de
  validade).
- `src/Estoque.Application` — `PagedResult<T>`, `IUnitOfWork`,
  `ITransactionScopeFactory`/`IDatabaseTransaction`, interfaces de integração
  (`IModuleAccessChecker`, `IFilialAccessChecker`+`FilialAccess`),
  **13 interfaces de repositório**, commands/queries records com padrão
  `WithTenant/WithContext` + validators FluentValidation + DTOs + serviços
  por contexto; `AddEstoqueApplication()` (validators por assembly).
- `src/Estoque.Infrastructure` — `EstoqueDbContext` +
  **14 configurações EF** (snake_case, soft delete named filter `"Active"`,
  índices únicos compostos/parciais por tenant, CHECK `quantity >= 0`),
  `ValueConverters` centralizados (nullable-safe), repositórios implementados,
  serviços de aplicação completos, **outbox** (`OutboxService`,
  `outbox_messages`), `TransactionScopeFactory` (transações explícitas nos
  fluxos de movimentação/importação), clientes Identity (`JwksKeyStore` +
  `JwksRefreshService` hosted, `IdentityApiClient` com forward do Bearer,
  `ModuleAccessChecker` fail-closed c/ cache TTL, `FilialAccessChecker`
  Allowed/Denied/Unknown), **4 BackgroundServices nativos**
  (`ExpiryScanWorker` 6h, `ReplenishmentScanWorker` 1h, `XmlImportWorker`
  polling 10s + parser NF-e minimalista, `OutboxDispatcherWorker` 30s),
  `EstoqueDbInitializer` (MigrateAsync no arranque) e
  `EstoqueDesignTimeDbContextFactory`; `AddEstoqueInfrastructure()`.
- `src/Estoque.Api` — `Program.cs` espelho do Identity (Serilog+CorrelationId,
  OpenAPI+Scalar dev-only, Prometheus `/metrics`, rate limit no upload XML,
  health checks Npgsql), JwtBearer **RS256 via JWKS remoto**
  (`IssuerSigningKeyResolver` sobre `JwksKeyStore`, sem I/O após 1º fetch),
  policies **`tenant`** (exige claim) e **`module-estoque`**
  (AuthorizationHandler + gate de módulo), `MapInboundClaims=false`
  (claims literais), controllers thin com base `EstoqueControllerBase`
  (ProblemDetails idênticos), extensão `GetRequiredTenantId()`;
  migrations auto-aplicadas no arranque.
- `tests/Estoque.Tests` — fixture Testcontainers Postgres real +
  `WebApplicationFactory` (RSA própria, fakes permissivos dos checkers),
  tokens com claims do Identity; smoke E2E.
- Docker: `Dockerfile.estoque` multi-stage non-root; compose com
  `estoque-postgres` + `estoque-api` (:8081) na rede `identity-net`
  (depends_on api healthy); `.env.example` estendido;
  `scripts/run-tests-in-docker.estoque.ps1` (contorna Smart App Control).

### Endpoints entregues

products · brands · models · categories · suppliers · stock/in|out|
adjustments|transfers|movements · balances · stock-rules · lots/expiring ·
outlet-items(+resolve) · purchase-suggestions(+decide) · xml-imports(upload/
status) · alerts(+acknowledge) · /api/health · /metrics.

## Correções técnicas relevantes (lições registradas)

1. **Colisão nome-de-propriedade × nome-de-tipo** (`BranchId.From(...)` dentro
   da entidade resolve para a propriedade em contexto estático) → chamadas
   qualificadas (`Common.XxxId`). PowerShell `-replace` é CASE-INSENSITIVE —
   cuidado com renames em lote.
2. **Conversores EF para props nullable**: usar `ValueConverter<TNão,Nulo?>`
   explícito (`ValueConverters.cs`); lambdas inferidas geram erro de provider.
3. **Nunca usar `.Value` de VO convertido dentro de árvore LINQ** — comparar a
   propriedade com constante convertida (`p.Sku == Sku.Create(x)`).
4. **`CreateAsyncScope()` não é awaitable** e não recebe CancellationToken.
5. **Program com try/catch engolidor quebra WebApplicationFactory** →
   fail-fast (`throw;` após Log.Fatal).
6. **MapInboundClaims=false** obrigatório para os nomes literais do contrato
   (`role`/`tenant_id`/`user_id`) — sem isso Roles= falha com 403 silencioso.
7. Migrations geradas via container Linux SDK (dotnet-ef 10.0.11) montando o
   workspace — Windows local bloqueia execução (WDAC).

## Validação

- `dotnet build Estoque.slnx` — **0 erros / 0 avisos** (inclui testes).
- `scripts/run-tests-in-docker.estoque.ps1` — **3/3 Passed**: health;
  401 sem token; fluxo completo (marca→produto→SKU duplicado 400→entrada 10@
  5,50→saída 11 = 400 invariante→saída 4→saldo 6 conferido em /balances com
  custo médio→transferência 2 p/ outra filial→Seller 403 em ajuste).

## Pendências / Próximos passos

- Ampliar suíte (lotes/validade, outlet, sugestões, importação XML E2E,
  dedupe de alertas via jobs forçados).
- `docs/CONTRATO-ESTOQUE.md` completo (rotas/payloads/exemplos curl).
- Subir stack integrada (`docker compose up -d --build`) e validar JWKS real
  Identity↔Estoque + cadastro do módulo slug `estoque` pelo SuperAdmin.
- Wire dos validators FluentValidation nas services (hoje: domínio + banco já
  cobrem as invariantes críticas; validators prontos para uso manual).
- Endpoint futuro no Identity: `GET /api/tenants/me/branches` (Etapa 23).
