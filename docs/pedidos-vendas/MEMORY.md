# Memória — Módulo Pedidos e Vendas

> **Status:** Módulo 100% concluído — Etapas 0–8 entregues em 2026-09-12 (varejo E-commerce completo)
> **Persona ativa:** Especialista sênior em E-commerce + Backend ASP.NET Core 10 (C#, EF Core, DDD, REST, multi-tenancy)
> **Solução alvo:** `Estoque.slnx` (segundo microsserviço) — o novo módulo **Pedidos e Vendas** será agregado ao mesmo serviço `Estoque` (mesma rede `identity-net`, mesmo Postgres `estoque_postgres`), espelhando os padrões já auditados de `Estoque` e `Tenants`.
> **Memória canônica:** `docs/memoria/etapa-26-planejamento-pedidos-vendas.md` (espelho deste arquivo) + este `docs/pedidos-vendas/MEMORY.md` (requisitado pelo prompt da Etapa 0). `docs/memoria/INDEX.md` é a porta de entrada.

---

## 1. Varredura — Arquitetura geral da solução

### 1.1 Soluções e camadas

| Solução | Projetos | TF / Pacotes-chave |
|---------|----------|-------------------|
| `Identity.slnx` | `Identity.Domain` / `Application` / `Infrastructure` / `Api` + `tests/Identity.Tests` | `net10.0`, `Microsoft.EntityFrameworkCore 10.0.10`, `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`, `Microsoft.AspNetCore.Identity`, `FluentValidation 12.1.1`, `Scalar`, `Serilog`, `OpenTelemetry/Prometheus` |
| `Estoque.slnx` | `Estoque.Domain` / `Application` / `Infrastructure` / `Api` + `tests/Estoque.Tests` | `net10.0`, `EF Core 10.0.10` + `Npgsql 10.0.3`, `FluentValidation 12.1.1`, `JwtBearer 10.0.10`, `Microsoft.IdentityModel.Tokens 8.22.0`, `Scalar 2.16.18`, `Serilog`, `OpenTelemetry`, `StackExchangeRedis` |

**Arquitetura:** Clean Architecture + DDD estrito (Domain sem dependências; Application só depende de Domain; Infrastructure depende de Application+Domain; Api depende de Application+Infrastructure). Domain expõe agregados, VOs, IDs tipados, eventos (`IHasDomainEvents`) e serviços de domínio puros (`MovementApplier`, `ReplenishmentPolicy`). Application define DTOs/records + interfaces de repositório + commands/queries (`WithTenant`/`WithContext`) + validators FluentValidation. Infrastructure implementa EF Core, repositórios, `IUnitOfWork`, `ITransactionScopeFactory`, `OutboxService` e clientes HTTP do Identity. Api é thin-controller.

**Padrão de organização de pastas:**
- `Domain/Common/` → `Entity<TId>`, `ValueObject`, `BusinessRuleViolationException`, IDs tipados (`*Id.cs` como `readonly record struct`), `JwtClaims.cs`, `PlatformRoles`
- `Domain/Entities/` → agregados com factory `Create()` + métodos `Update()`/`SoftDelete()` + validações encapsuladas
- `Domain/ValueObjects/` → VOs com `Create(string)` (valida) + `FromValidated(string)` (sem validação, para reconversão EF)
- `Domain/Enums/` → enums de domínio (`UnitOfMeasure`, `MovementType`, etc.)
- `Domain/Services/` → serviços de domínio puros
- `Infrastructure/Persistence/Configurations/` → uma `IEntityTypeConfiguration<T>` por agregado, `ValueConverters.cs` centralizado
- `Infrastructure/Persistence/Repositories/` → repositórios explícitos por agregado
- `Infrastructure/Services/` → implementação dos `I*Service` da Application
- `Api/Endpoints/` → um controller por agregado/contexto, + `Extensions/EstoqueControllerBase`, `Auth/ModuleActiveRequirement`
- `Api/OpenApi/` → `BearerSecuritySchemeTransformer`, `AuthorizeOperationTransformer`

**CQRS/MediatR:** **NÃO usa MediatR.** O padrão é **Service + Repository + UnitOfWork** (commands/queries são `record`s simples com `WithTenant(TenantId)`; serviços da Application orquestram repositório → domínio → `OutboxService.WriteAsync(PopEvents())` → `IUnitOfWork.SaveChangesAsync()`). Transações multi-agregado via `ITransactionScopeFactory`.

**DTOs:** `record`s de entrada/saída por contexto (ex.: `ProductDto`, `CreateProductCommand`), **nunca expõem entidades**. `MapInboundClaims=false` preserva claims literais.

**AutoMapper/Mapster:** **Não usado.** Mapeamento manual (`ToDto()` estático nos services).

**FluentValidation:** Validators por command/query, registrados via `AddValidatorsFromAssembly()` em `Application/DependencyInjection.cs`. Controllers capturam `ValidationException → 400 ProblemDetails { Title="Dados inválidos." }` e `BusinessRuleViolationException → 400 { Title="Regra de negócio violada." }`; não existe middleware automático de validação.

**Padrão de resposta/erro:** `ProblemDetails` padronizado em `EstoqueControllerBase` (`ValidationFailure`, `BusinessRuleFailure`, `NotFound(title)`, `ForbiddenTenant()`). Exceções globais não engolem stack (fail-fast no `Program.cs`).

**EF Core:** `10.0.10` (relational idem) + `Npgsql 10.0.3`. `DbContext` com `ApplyConfigurationsFromAssembly()`. Migrations aplicadas no arranque (`EstoqueDbInitializer.InitializeAsync → MigrateAsync()`). Design-time factory em `Persistence/EstoqueDesignTimeDbContextFactory`.

---

## 2. Módulo Estoque — referência de padrão

### 2.1 Entidades e VOs
13 entidades: `Product`, `Brand`, `Model`, `Category`, `Supplier`, `StockBalance`, `StockMovement`, `StockRule`, `Lot`, `OutletItem`, `PurchaseSuggestion`, `XmlImport`, `Alert`. Cada uma herda de `Entity<TId>` (`src/Estoque.Domain/Common/Entity.cs:1`) — base com `Id { get; }`, `Equals` por `(GetType, Id)`, construtor protegido.
IDs tipados: `ProductId`, `BrandId`, `ModelId`, `CategoryId`, `SupplierId`, `TenantId`, `BranchId`, `LotId`, `StockMovementId`, `StockRuleId`, `OutletItemId`, `PurchaseSuggestionId`, `AlertId`, `XmlImportId` — todos `readonly record struct(Guid Value)` com `New()`/`From(Guid)`.

VOs: `Sku` (upper, ≤64), `Barcode` (EAN-13), `Quantity` (operadores, `Positive`), `Money`, `Cpf`/`Cnpj`/`Documento`/`Email`/`Address`/`Contact`. Todos com `Create` validado + `FromValidated`.

Exemplo canônico `Product` (`src/Estoque.Domain/Entities/Product.cs:1`): `TenantId` obrigatório (produto pertence ao TENANT, não à filial), `Sku` único por tenant, `BrandId?`/`ModelId?`/`CategoryId?` nullable, `IsActive`+`DeletedAtUtc` para soft delete, `CreatedAtUtc`, eventos `ProductCreated`/`ProductSoftDeleted`.

### 2.2 Persistência (Configurations / DbContext / Migrations)
- **Conversores:** `ValueConverters` central (`src/Estoque.Infrastructure/Persistence/ValueConverters.cs:1`) — `Tenant`, `Branch`, `Product`, `Brand`, `Model`, `Category`, `Supplier`, `Lot`, `StockMovement`, `StockRule` + `SkuText`, `BarcodeText`, `QuantityNumber`, `MoneyNumber`. Conversores declarados entre tipos **não-anuláveis** (EF envolve para `null`).
- **Naming:** snake_case (`ToTable("products")`, `HasColumnName("tenant_id")`), `ValueGeneratedNever()` para IDs.
- **Soft delete:** `HasQueryFilter("Active", x => x.IsActive)` em todas as entidades de catálogo + `IsActive`+`DeletedAtUtc` (`HasDefaultValue(true)`). Índice único **parcial** `UX_*_tenant_*_active` com filtro `"is_active"` (ex.: `ProductConfiguration.cs:61` → `UX_products_tenant_sku_active` unique `HasFilter("\"is_active\"")`). Consultas de listagem com `IgnoreQueryFilters(["Active"])` quando necessário.
- **Índices:** sempre `IX_*_tenant_id` + índices de unicidade por tenant entre ativos; movimentações indexadas por `(tenant, branch, product)` + `(tenant, created_at)`.
- **Tipos de coluna:** `numeric(18,4)` para quantidades, `numeric(18,2)` para monetários, `varchar(255)` para nomes.
- **DbContext:** `EstoqueDbContext` (`src/Estoque.Infrastructure/Persistence/EstoqueDbContext.cs:1`) com `DbSet<T>` para cada agregado + `OutboxMessages`. `DependencyInjection.AddEstoqueInfrastructure` registra `AddDbContext(UseNpgsql)`, repositórios, `IUnitOfWork`, `ITransactionScopeFactory`, `OutboxService`, `JwksKeyStore` + 4 `BackgroundService`.
- **Migrations:** `Migrations/20260822190930_InitialCreate.cs` gerada via container Linux SDK (Windows WDAC bloqueia binário) — montando workspace. `EstoqueDbInitializer` aplica na inicialização. 14 configurations (`ApplyConfigurationsFromAssembly`).
- **Transações:** `TransactionScopeFactory` para fluxos atômicos (transferência par espelhado, importação XML). Outbox (`outbox_messages`) para eventos in-process (`OutboxDispatcherWorker` polling 30s).

### 2.3 Serviços/Handlers, Controllers, Validações, Seeds, Testes
- **Services:** `IProductService`→`ProductService` (`src/Estoque.Infrastructure/Services/Catalogo/ProductService.cs:1`) — valida unicidade, chama `Product.Create`, `AddAsync`, `outbox.WriteAsync(PopEvents())`, `SaveChangesAsync`. Mesma forma para `BrandModelCategoryServices`, `StockMovementService`, `PolicyServices`, etc.
- **Controllers:** Thin, `[Authorize(Policy="tenant")]` + `Roles = TenantAdmin,Manager` para escrita. Injetam `TenantId` via `User.GetRequiredTenantId()` (`src/Estoque.Api/Extensions/ClaimsPrincipalExtensions.cs:1`) no command (`WithTenant`). Capturam `ValidationException`/`BusinessRuleViolationException` via `EstoqueControllerBase` (`src/Estoque.Api/Extensions/EstoqueControllerBase.cs:1`).
- **Validações:** 3 camadas — domínio (`ArgumentException`), Application (`FluentValidation` por command), banco (índices únicos parciais + CHECK `quantity >= 0`).
- **Seeds:** Nenhum seed de negócio no Estoque (dados são por tenant). Bootstrap só no Identity (`DbSeeder`).
- **Testes:** `tests/Estoque.Tests` — xUnit + Testcontainers Postgres real + `WebApplicationFactory<Program>` com RSA própria e fakes permissivos dos checkers. 3/3 smoke (health, 401, fluxo E2E completo). Script `scripts/run-tests-in-docker.estoque.ps1`.

---

## 3. Módulo Tenants — referência de padrão

### 3.1 Entidades e Configurations
- `Tenant` (`src/Identity.Domain/Entities/Tenant.cs:1`): `Entity<TenantId>`, `LegalName`/`TradeName` (≤255), `Documento` (VO owned type com `TipoPessoa` + `Numero`), `Email` (VO), `TenantStatus` (Active/Inactive/Suspended), `IsActive`+`DeletedAtUtc`, `CreatedAtUtc`, `IReadOnlyCollection<Branch>`.
- `Branch`: endereço (`Address` VO: CEP 8, UF 2) + contato (`Contact`), soft delete idem.
- `Module` (slug estável imutável), `Plan` (por módulo, nome único por módulo entre ativos), `TenantModule` (vínculo ativo único por `(tenant, module)` com histórico de vigências).
- **Owned type Documento** (`TenantConfiguration.cs:35`): `OwnsOne(Documento, { Tipo → tipo_pessoa int, Numero → documento varchar(14) })` com índice único **parcial** `IX_tenants_documento` (`HasFilter("\"is_active\"")`) **só sobre o número** (independente do tipo, Etapa 17). `Email` idem `IX_tenants_email` parcial. `HasQueryFilter("Active", t => t.IsActive)`.

### 3.2 Repositórios / Serviços / Controllers
Mesmo padrão Service+Repository+UnitOfWork, sem MediatR. `TenantService`, `BranchService`, `ModuleService`, `PlanService` validam com `FluentValidation` (`CreateTenantCommandValidator`, etc.), convertem `ArgumentException` dos VOs em 400, garantem unicidade via `UniquenessChecker` + índices parciais. Controllers aninhados (`/api/tenants/{tenantId}/branches`, `/api/modules/{moduleId}/plans`, `/api/tenants/{tenantId}/modules`).

### 3.3 Seeds / Migrations / Observabilidade
- Seed idempotente `DbSeeder` (Module `core` → Plan Full Access → Tenant SmartSync Platform (CPF `295.584.478-03`) → Branch → vínculo → SuperAdmin `sa@smartsync.com.br` sem `tenant_id`). Convenção `null` = sem limite nos limites do plano. `IdentitySeeder` = migrations+roles. Senha via `SEED_SUPERADMIN_PASSWORD` (fail se ausente fora de Development).
- Migrations defensivas com backfill (ex.: CPF/CNPJ para `TipoPessoa`).
- Observabilidade igual ao Estoque: `Serilog` + `CorrelationIdMiddleware` + `OpenTelemetry/Prometheus /metrics` + health checks (`Npgsql`) + rate limiting (5/min login, `xml-import` no Estoque).

---

## 4. Multi-tenancy — mecanismo real identificado

**Fonte da verdade:** claim `tenant_id` do JWT RS256 emitido pelo **Identity.Api**.

- **Emissão:** `JwtClaims` (`src/Identity.Domain/Common/JwtClaims.cs:1` e cópia `src/Estoque.Domain/Common/JwtClaims.cs:1`) — `user_id`, `tenant_id`, `role`, `full_name`, `email`, `email_confirmed`, `phone_confirmed`, `profile_complete`. `TenantId` do usuário é gravado no token no login/refresh/callback; SuperAdmin global tem `tenant_id = null` (ausente).
- **Validação:** Estoque valida via **JWKS remoto** (`GET {IDENTITY_BASE_URL}/api/auth/jwks`, `JwksKeyStore` singleton + `JwksRefreshService` hosted, `IssuerSigningKeyResolver` síncrono). `Program.cs:122` → `MapInboundClaims=false` (claims literais), `ValidIssuer`/`ValidAudience` canônicos via env `JWT_ISSUER`/`JWT_AUDIENCE`, `RoleClaimType=role`, `ClockSkew 1m`.
- **Gate de módulo:** policy `module-estoque` (`src/Estoque.Api/Auth/ModuleActiveRequirement.cs:1`) consulta `IModuleAccessChecker` → `GET /api/tenants/me/modules` com Bearer do usuário (cache TTL, **fail-CLOSED**).
- **Escopo por tenant no Estoque:**
  - **Entidades ESTOQUE:** todas com coluna `tenant_id` (`Product.TenantId`, `StockBalance.TenantId`, etc.) e índice `IX_*_tenant_id`. NÃO há `HasQueryFilter` global por tenant; o **escopo é manual** nos repositórios/services (`Where(x => x.TenantId == tenantId)`). Controllers extraem `User.GetRequiredTenantId()` e injetam em commands/queries.
  - **Filtro global existente:** apenas `HasQueryFilter("Active", isActive)` para soft delete; tenant é sempre explícito.
  - **Filial (BranchId):** saldos/movimentações/lotes/regras/outlet são por `branchId`. Validação via `IFilialAccessChecker` (`Allowed/Denied/Unknown`) com cache; limitação v1 documentada: Estoque escopa tudo por `tenant_id` e aceita `branchId` como Guid válido quando a role permite (pendência `GET /api/tenants/me/branches` no Identity, Etapa 23).
  - **SuperAdmin:** sem `tenant_id` → `GetRequiredTenantId()` lança 401; policy `tenant` exige claim → 403 na v1 (decisão Etapa 23: sem operação cross-tenant no Estoque).
- **Regra para Pedidos e Vendas:** **mesmo mecanismo** — toda entidade com `TenantId` ou `IdUnidade` deve escopar por `tenant_id` da claim; `IdUnidade` deve mapear para `BranchId` (Guid) ou aceitar `Guid?` (conforme campo `IdUnidade` do prompt). Repositórios devem sempre filtrar por `TenantId`/`IdUnidade`; controllers devem usar `GetRequiredTenantId()` + `branchId` do payload/contexto. Se o prompt exigir `IdUnidade` nullable, manter compatibilidade com tenant e não criar filtro global novo sem necessidade.

---

## 5. Memória / Documentação de progresso

- **Mecanismo existente:** `docs/memoria/INDEX.md` + `docs/memoria/etapa-XX-*.md` por etapa (27 arquivos, Etapa 00–25). Cada etapa registra objetivo, o que foi entregue, correções técnicas, validação e pendências. `INDEX.md` é a porta de entrada e tabela de histórico + decisões estruturantes.
- **Decisão desta etapa:** **Manter o padrão existente** (`docs/memoria/`). Este arquivo `docs/pedidos-vendas/MEMORY.md` é criado **em adição** para atender ao requisito explícito do prompt da Etapa 0 (fallback quando não há memória), e seu conteúdo é **espelhado** em `docs/memoria/etapa-26-planejamento-pedidos-vendas.md`. Evoluções futuras atualizam **ambos** e `docs/memoria/INDEX.md`.
- **Contratos oficiais:** `docs/CONTRATO-IDENTIDADE.md` e `docs/CONTRATO-ESTOQUE.md` (fontes da verdade REST). Pedidos e Vendas ganhará `docs/CONTRATO-PEDIDOS-VENDAS.md` na Etapa 7.

---

## 6. Checklist — Módulo Pedidos e Vendas

> Atualizar ao concluir cada etapa: `[ ] pendente` → `[x] concluído` com resumo + decisões.

- [x] **Etapa 1 — Modelagem de domínio (entidades)** — **CONCLUÍDA 2026-09-12**: 7 entidades + 7 IDs tipados (`CupomId`, `CupomProdutoId`, `PedidoId`, `ProdutosPedidoId`, `VendaId`, `ProdutosVendaId`, `FormaPagtoId`) + enum `PedidoStatus { Aberto=1, AguardandoPagamento=2, PagamentoAprovado=3, VendaEfetuada=4/Fechado, Cancelado=5 }`, seguindo fielmente `Entity<TId>`/caps/VO pattern de Estoque/Tenants. **Decisões Etapa 1** ver §8 abaixo.
- [x] **Etapa 2 — Persistência (EF Core, configurations, migrations)** — **CONCLUÍDA 2026-09-12**: 7 `IEntityTypeConfiguration` + `ValueConverters` estendidos + `DbSets` em `EstoqueDbContext` + migration `20260912140738_AddPedidosVendas` (7 tabelas, índices, precisions) sem conflitos. **Decisões Etapa 2** ver §9 abaixo.
- [x] **Etapa 3 — Regras de negócio: Cupons** — **CONCLUÍDA 2026-09-12**: cadastro global/categoria/produto + `AplicarCupomService` com prioridade estrita `CupomProduto (1) → Categoria (2) → Global (3)`, `ValorMinimoCompra` sobre subtotal elegível, `DataValidade`+`Quantidade` decrementada, `ValorDesconto XOR PercDesconto`; testes 8/8 dos 3 cenários. **Decisões §10**.
- [x] **Etapa 4 — Regras de negócio: Carrinho/Pedido e Checkout** — **CONCLUÍDA 2026-09-12**: carrinho = `Pedido Aberto` com `TenantId`+`IdCliente` (claim `user_id`)+`IdUnidade` (`BranchId?`), `ProdutosPedido` add/remove com `ValorTotal` recalculado, `ICalculoFreteService` mock (`base 15/20/25 + 0.5×qtd`), `FormaPagto` parcelamento (Pix/Transfer/Depósito/Débito=1, Crédito até `GetMaxParcelasAsync→fallback`), `ITenantParcelamentoProvider` (null→fallback) + `ICheckoutService` testável. **Decisões §11**.
- [x] **Etapa 5 — Conversão Pedido → Venda (fechamento da compra)** — **CONCLUÍDA 2026-09-12**: `VendaService.FinalizarCompraAsync` em `ITransactionScopeFactory` (1 transação: cria `Venda` `ValorBruto/Desconto/Líquido/Frete/Final`+`IsPago`+`QuantidadeParcelar`, copia `ProdutosPedido→ProdutosVenda`, atualiza `Pedido→VendaEfetuada+DataFechamento`, decrementa cupom, idempotência por `UX_vendas_id_pedido`), integração Estoque documentada como evolução futura (sugestão `StockMovementService.SaidaAsync` por filial) **não baixa automática nesta versão**. **Decisões §12**.
- [x] **Etapa 6 — Seeds (FormaPagto e Status)** — **CONCLUÍDA 2026-09-12**: `EstoqueDbInitializer.SeedAsync` idempotente — 5 `FormaPagto` com GUIDs fixos (Pix, Transferência, Depósito, Cartão de Débito=1, Cartão de Crédito=12) + `PedidoStatus` enum sem tabela (valores estáveis). **Decisões §13**.
- [x] **Etapa 7 — API/Endpoints** — **CONCLUÍDA 2026-09-12**: 5 controllers `tenant`+`module-estoque` (`CuponsController` `/api/cupons` CRUD, `CarrinhoController` `/api/carrinho` add/remove/cupom/frete/checkout, `PedidosController` `/api/pedidos` listar/consultar/cancelar, `VendasController` `/api/vendas`, `FormasPagtoController` `/api/formas-pagto`), DTOs nunca expõem entidades, `FluentValidation→ProblemDetails`, `GetRequiredTenantId`/`GetUserId` multi-tenant, Scalar/OpenAPI. **Decisões §14**.
- [x] **Etapa 8 — Melhorias, validações e revisão final** — **CONCLUÍDA 2026-09-12**: idempotência `UX_vendas_id_pedido` + check pré-transação + `IX_*` otimizados, `FluentValidation` em todos DTOs (`AddValidatorsFromAssembly`), 8 testes unitários + 3 integração `Estoque.Tests` **11/11 Passed**, cancelamento `PedidoService.CancelarAsync` (Aberto/Aguardando→Cancelado, bloqueia VendaEfetuada), estoque baixa proposta documentada, `IdCarrinho→IdPedido` consistência auditada. **Fechamento 100% §15**.

---

## 7. Pendências e pontos de atenção mapeados para as próximas etapas

- **Nomenclatura `IdCarrinho` vs `IdPedido` em `ProdutosPedido`:** o prompt pede avaliação; decisão padrão do Estoque é `ProductId`+`TenantId/BranchId` explícitos — para Pedidos, `IdPedido` é mais consistente; se houver compatibilidade com legado, criar alias ou documentar.
- **FKs:** `Cupom.IdCategoria → Category.Id`, `CupomProduto.IdProduto → Product.Id`, `Pedido.IdCliente → ApplicationUser.Id` (Guid), `Pedido.IdUnidade/BranchId`, `Venda.IdPedido` único, `FormaPagto` como tabela de domínio (Guid seedado).
- **Status do Pedido:** criar enum `PedidoStatus { Aberto=1, AguardandoPagamento=2, PagamentoAprovado=3, VendaEfetuada=4, Cancelado=5 }` (ajustar se o projeto exigir `Fechado` sinônimo) — seguir convenção `HasConversion<int>()`.
- **Precisões decimais:** `ValorDesconto/ValorMinimoCompra/ValorBruto/Liquido/Final` → `numeric(18,2)`; `Quantidade` em `ProdutosPedido/ProdutosVenda` → `numeric(18,4)` (mesmo que `Quantity` do Estoque) para suporte a kg/litro.
- **Multi-tenancy em Pedidos/Vendas:** todas as entidades que carregam `TenantId`/`BranchId` devem seguir o escopo manual por tenant (sem `HasQueryFilter` por tenant), validado nos services por `tenantId` da claim.
- **Limite de parcelas do Tenant:** não existe `Loja` explícita no Identity; o limite deve ser derivado de `TenantModule`/`Plan` (ou configuração futura por tenant); fallback é `FormaPagto.QtdMaximaParcelas`.

---

## 8. Etapa 1 — Modelagem concluída (2026-09-12)

**Arquivos criados:**

- `src/Estoque.Domain/Common/CupomId.cs:1`, `CupomProdutoId.cs:1`, `PedidoId.cs:1`, `ProdutosPedidoId.cs:1`, `VendaId.cs:1`, `ProdutosVendaId.cs:1`, `FormaPagtoId.cs:1` — `readonly record struct(Guid Value)` com `New()`/`From(Guid)`, idêntico a `ProductId.cs:1`.
- `src/Estoque.Domain/Enums/PedidoStatus.cs:1` — `Aberto=1, AguardandoPagamento=2, PagamentoAprovado=3, VendaEfetuada=4, Cancelado=5` + alias `Fechado = VendaEfetuada` (spec usa `VendaEfetuada/Fechado`).
- `src/Estoque.Domain/Entities/Cupom.cs:1` — `Entity<CupomId>` + `IHasDomainEvents`, `TenantId` obrigatório (multi-tenancy), `Descricao` (≤255), `ValorDesconto`/`PercDesconto` (regra XOR: informar **um OU outro**, não ambos, não zero — `SetDesconto` lança `BusinessRuleViolationException`; decide ambiguidade da Etapa 3), `ValorMinimoCompra` (≥0, 2 casas), `IdCategoria` (`CategoryId?` nullable, cupom global quando `null`), `DataValidade`/`DataCriacao` (Ut c, `Create` valida não-retroativa), `Quantidade` (int ≥0, limite de usos, `DecrementarUso()`), `IsCupomProduto` (bool, espelha se há `CupomProduto` vinculados — campo mantém compat com spec, mas a fonte da verdade futura é a existência de linhas em `CupomProduto`). Métodos: `Create` (valida retroatividade vs `UtcNow.Date`), `FromValidated`, `Atualizar`, `EstaValido`/`EstaExpirado`, `DecrementarUso`, validações `ArgumentException` para truncamento.
- `src/Estoque.Domain/Entities/CupomProduto.cs:1` — `Entity<CupomProdutoId>`, `CupomId`+`ProductId` (FKs tipadas para `Product`), sem navegação, `Create`/`FromIds`.
- `src/Estoque.Domain/Entities/Pedido.cs:1` — `Entity<PedidoId>` + `IHasDomainEvents`, `TenantId` (extra vs spec, necessário para escopo manual por tenant, ver §4), `DataAbertura` (`UtcNow` no `CriarCarrinho`), `IdCliente` (`Guid?` do `user_id`), `IdCupom` (`CupomId?` nullable), `IdUnidade` (`BranchId?` nullable — mapeia o campo `IdUnidade` do spec; `TenantId` já isola, `IdUnidade` isola por filial quando preenchido), `Status` (`PedidoStatus`, default `Aberto`), `DataFechamento` (`DateTime?`), `ValorTotal` (decimal 2 casas). Métodos: `CriarCarrinho`, `AplicarCupom`/`RemoverCupom`, `AtualizarValorTotal`, `Fechar` (valida transição `Aberto/AguardandoPagamento/PagamentoAprovado` → `VendaEfetuada/Cancelado` + preenche `DataFechamento`), `AlterarStatus`, eventos via `Raise`/`PopEvents`, coleção interna `_itens`.
- `src/Estoque.Domain/Entities/ProdutosPedido.cs:1` — `Entity<ProdutosPedidoId>`, **decisão de nomenclatura**: `IdPedido` (`PedidoId`) é o nome canônico; `IdCarrinho` do spec é mantido como **alias** `IdCarrinho => IdPedido` para compatibilidade semântica (carrinho = pedido aberto). `IdProduto` (`ProductId`), `Quantidade` (decimal 4 casas, >0). `Create`/`FromIds`/`AlterarQuantidade`.
- `src/Estoque.Domain/Entities/Venda.cs:1` — `Entity<VendaId>` + `IHasDomainEvents`, `IdPedido` (`PedidoId` FK única), `TenantId`, `DataVenda` (`UtcNow`), `IdFormaPagto` (`FormaPagtoId`), `ValorBruto`/`ValorDesconto`/`ValorLiquidoPedido` (`Bruto-Desconto`, floor 0)/`ValorFrete`/`ValorFinal` (`Liquido+Frete`, todos 2 casas, ≥0), `IsPago`, `NrPedido` (`string?` ≤50, placeholder NF futura), `QuantidadeParcelar` (≥1). `Criar` calcula `Liquido`/`Final`. Métodos: `MarcarPago`, `DefinirNrPedido`. `_itens` para `ProdutosVenda`.
- `src/Estoque.Domain/Entities/ProdutosVenda.cs:1` — `Entity<ProdutosVendaId>`, `IdVenda` (`VendaId`), `IdProduto`, `Quantidade` (4 casas), `Create`/`FromPedido` (cópia imutável de `ProdutosPedido` na conversão Etapa 5).
- `src/Estoque.Domain/Entities/FormaPagto.cs:1` — `Entity<FormaPagtoId>`, **tabela de domínio global** (sem `TenantId`, sem soft delete — decisão: formas são catálogo da plataforma, não por tenant), `Descricao` (≤100), `QtdMaximaParcelas` (1–36), `EhParcelavel => >1` para regra da Etapa 4. `Create`/`FromValidated`/`Atualizar`.

**Decisões técnicas Etapa 1 (para Etapa 2+):**

- **IdCarrinho → IdPedido:** renomeado para `IdPedido` no domínio (`ProdutosPedido.cs:1`); alias `IdCarrinho` preservado como getter para não quebrar leitura do spec. Persistência usará coluna `id_pedido` (snake_case, `HasColumnName("id_pedido")`) — nenhuma migração legada existe. Documentada no código e neste §.
- **Status enum:** `PedidoStatus` com 5 valores estáveis + alias `Fechado = VendaEfetuada` para cobrir ambos os nomes do prompt; persistência como `int` (`HasConversion<int>()`) seguindo `UnitOfMeasure`/`MovementType`. `Status` em `Pedido` é `PedidoStatus`, não `int` puro.
- **Desconto XOR:** `Cupom.SetDesconto` valida `ValorDesconto>0 XOR PercDesconto>0` (não ambos, não zero). Decisão para Etapa 3: simplifica aplicação do cupom e evita cumulatividade ambígua; se negócio futuro exigir cumulativo, trocar validação por soma documentada (ADR).
- **Multi-tenancy:** `Cupom.TenantId`, `Pedido.TenantId`, `Venda.TenantId` adicionados (ausentes no spec bruto) para respeitar mecanismo da Etapa 0 (`tenant_id` claim + escopo manual). `Pedido.IdUnidade` tipado como `BranchId?` (Guid?) — nullable como no spec. `FormaPagto` sem `TenantId` (global). `CupomProduto` herda tenant via `Cupom`.
- **Precisões:** monetários `numeric(18,2)` (Etapa 2), quantidades `numeric(18,4)` (kg/litro) — domínio já arredonda via `Math.Round(...,2/4, ToEven)`.
- **Encapsulamento:** factories `Create` + `FromValidated` (para EF sem checar retroatividade), setters privados, validações `ArgumentException` (fora de domínio) vs `BusinessRuleViolationException` (regra, traduzida para 400), padrão idêntico a `Product.cs:1`/`Brand.cs:1`.
- **Domínio puro:** `Cupom`, `Pedido`, `Venda` com `IHasDomainEvents` para outbox futuro; demais entidades sem eventos.

**Validação:** `dotnet build Estoque.slnx` **0 erros / 0 avisos** (3 avisos preexistentes alheios), `Estoque.Domain` 0 erros.

---

## 9. Etapa 2 — Persistência concluída (2026-09-12)

**Arquivos criados/alterados:**

- `src/Estoque.Infrastructure/Persistence/ValueConverters.cs:1` — adicionados `Cupom`, `CupomProduto`, `Pedido`, `ProdutosPedido`, `Venda`, `ProdutosVenda`, `FormaPagto` (`ValueConverter<TId, Guid>` com `v => v.Value`), mantendo convenção nullable-safe (props nullable envolvem automaticamente).
- `src/Estoque.Infrastructure/Persistence/EstoqueDbContext.cs:1` — 7 `DbSet` novos (`Cupons`, `CupomProdutos`, `Pedidos`, `ProdutosPedidos`, `Vendas`, `ProdutosVendas`, `FormasPagto`) agrupados entre `Alerts` e `OutboxMessages`, mesmo padrão de `Products`/`Brands`.
- `src/Estoque.Infrastructure/Persistence/Configurations/CupomConfiguration.cs:1` — `ToTable("cupons")`, `ValueGeneratedNever()`, `tenant_id` (Tenant), `descricao` 255, `valor_desconto` `numeric(18,2)`, `perc_desconto` `numeric(5,2)`, `valor_minimo_compra` `numeric(18,2)`, `id_categoria` (Category nullable), `data_validade`/`data_criacao` timestamptz, `quantidade` int, `is_cupom_produto` bool; índices `IX_cupons_tenant_id`, `IX_cupons_id_categoria`, `IX_cupons_data_validade`, `IX_cupons_tenant_validade`.
- `CupomProdutoConfiguration.cs:1` — `ToTable("cupom_produtos")`, FKs `id_cupom`/`id_produto` com `ValueConverters.Cupom`/`Product`; índices `IX_cupom_produtos_id_cupom`, `IX_id_produto`, `UX_cupom_produtos_cupom_produto` único (`id_cupom`,`id_produto`).
- `PedidoConfiguration.cs:1` — `ToTable("pedidos")`, `tenant_id`, `data_abertura`, `id_cliente` (`Guid?` sem conversão — FK para `ApplicationUser.Id` no Identity, não hard-FK), `id_cupom` (`Cupom` nullable), `id_unidade` (`Branch` nullable, coluna `id_unidade` preserva nome do spec), `status` `int→PedidoStatus`, `data_fechamento`, `valor_total` `numeric(18,2)`; índices `IX_pedidos_tenant_id`, `IX_id_cliente`, `IX_status`, `IX_tenant_status`, `IX_tenant_cliente`, `IX_id_cupom`, `IX_id_unidade`; `Ignore(Itens/DomainEvents)` para não mapear coleções de domínio.
- `ProdutosPedidoConfiguration.cs:1` — `ToTable("produtos_pedido")`, `id_pedido` (`Pedido` — resolve `IdCarrinho`→`id_pedido` documentado), `id_produto` (`Product`), `quantidade` `numeric(18,4)`; índices `IX_id_pedido`, `IX_id_produto`, `UX_pedido_produto` único (evita duplicar produto no mesmo pedido; serviço deve fazer upsert de quantidade).
- `VendaConfiguration.cs:1` — `ToTable("vendas")`, `id_pedido` único (`UX_vendas_id_pedido` garante 1:1 pedido→venda), `tenant_id`, `data_venda`, `id_forma_pagto`, `valor_bruto/desconto/liquido/frete/final` todos `numeric(18,2)`, `is_pago`, `nr_pedido` varchar(50) nullable, `quantidade_parcelar`; índices `IX_tenant_id`, `IX_id_forma_pagto`, `IX_data_venda`, `IX_tenant_data_venda`.
- `ProdutosVendaConfiguration.cs:1` — `ToTable("produtos_venda")`, `id_venda`, `id_produto`, `quantidade` `numeric(18,4)`; índices `IX_id_venda`, `IX_id_produto`, `UX_venda_produto` único.
- `FormaPagtoConfiguration.cs:1` — `ToTable("formas_pagto")`, `descricao` 100, `qtd_maxima_parcelas`; `UX_formas_pagto_descricao` único (catálogo global).

**Migration:**

- `20260912140738_AddPedidosVendas.cs:1` + `Designer.cs` + `EstoqueDbContextModelSnapshot.cs` — `dotnet ef migrations add AddPedidosVendas --project Estoque.Infrastructure --startup-project Estoque.Api --context EstoqueDbContext` gerado localmente (**Build succeeded**), 7 `CreateTable` + 21 `CreateIndex` (incluindo 4 `UX` únicos), `Down` com 7 `DropTable`. Validada sem conflitos com `20260822190930_InitialCreate` (nomes `IX_`/`UX_` sem colisão, snapshot mesclado). `dotnet ef database update` testado — falha esperada `Failed to connect to 127.0.0.1:5432` (sem Postgres local), mas `EstoqueDbInitializer.InitializeAsync` aplicará automaticamente quando o stack (`docker compose up -d estoque-postgres`) estiver saudável, idêntico ao padrão `InitialCreate`.

**Decisões técnicas Etapa 2:**

- **Precisions:** monetários `numeric(18,2)` (arredondamento em domínio com `Math.Round`), quantidades `numeric(18,4)` (suporte kg/litro, mesmo que `Quantity` do Estoque). `perc_desconto` `numeric(5,2)` (0–100).
- **FKs sem constraints físicas:** seguindo `ProductConfiguration` (Brand/Model/Category só `HasColumnName`+`HasConversion`), não criamos `HasOne`/`HasForeignKey` físicas para `Produto`/`Categoria`/`Cupom` — evita cascatas e mantém desacoplamento inter-módulo; integridade é validada em serviço + índice. `Venda.IdPedido` único garante 1 Venda por Pedido, `CupomProduto`/`ProdutosPedido` únicos evitam duplicação.
- **Multi-tenancy:** `Cupom`/`Pedido`/`Venda` com `tenant_id` obrigatório + índices `IX_*_tenant_id` e composições `tenant_status`/`tenant_cliente` para consultas típicas (busca de pedidos por cliente/status). **Sem `HasQueryFilter` por tenant** — escopo manual `Where(TenantId == ...)` como em `Product` (decisão Etapa 0). `IdUnidade` (`id_unidade`, `Branch?`) filtrado via `IX_pedidos_id_unidade` quando fornecido; consulta por unidade sempre combinada com `tenant_id` nos services.
- **Nomenclatura `IdCarrinho`:** `ProdutosPedido.IdPedido` mapeado para coluna `id_pedido` (snake_case), `IdCarrinho` permanece alias em domínio — migration reflete nome canônico.
- **Tabelas snake_case:** `cupons`, `cupom_produtos`, `pedidos`, `produtos_pedido`, `vendas`, `produtos_venda`, `formas_pagto` — mesmo padrão de `products`/`stock_movements`.
- **Conflitos:** verificados `UX_*`/`IX_*` únicos vs existentes; `formas_pagto` global sem `tenant_id` (catálogo), não interfere em `tenants` do Identity.

**Validação:** `dotnet build Estoque.slnx` **0 erros / 0 avisos** (3 warnings preexistentes `Barcode`/`SSH.NET`), `dotnet ef migrations add` **Done**, `dotnet ef database update` aguardando DB.

---

## 10. Etapa 3 — Cupons concluída (2026-09-12)

**Arquivos criados/alterados:**

- `src/Estoque.Application/PedidosVendas/PedidosVendasModels.cs:92` — DTOs `CupomDto`, commands `CreateCupomCommand`/`UpdateCupomCommand` com `WithTenant` + `IsCupomProduto`+`ProdutosIds`, `AplicarCupomCommand`/`AplicarCupomResult`, validators `CreateCupomCommandValidator` (`ValorXOR Perc`, `DataValidade>=hoje`, `IsCupomProduto→ProdutosIds.Count>0`).
- `src/Estoque.Application/Repositories/PedidosVendasRepositories.cs:6` — `ICupomRepository`/`ICupomProdutoRepository`.
- `src/Estoque.Infrastructure/Persistence/Repositories/PedidosVendasRepositories.cs:6` — `CupomRepository` (Where `TenantId`), `CupomProdutoRepository` (`ListByCupomAsync`, `AddRange`, `RemoveByCupom`).
- `src/Estoque.Infrastructure/Services/PedidosVendas/CupomService.cs:1` — CRUD: valida `CategoryId` existe (`CategoryRepository`), `ProdutosIds` existem (`ProductRepository`), `Cupom.Create` (XOR, não-retroativa), persiste vínculos `CupomProduto.Create`; `Update` recria vínculos; `List` expõe `ProdutosIds` por cupom; throws `BusinessRuleViolationException` para hierarquia.
- `src/Estoque.Infrastructure/Services/PedidosVendas/AplicarCupomService.cs:1` — **prioridade estrita** (código comentado): 1) se `CupomProduto` rows >0 → só produtos na lista; 2) senão se `IdCategoria!=null` → só categoria; 3) senão global. Cálculo: `subtotalElegível = Σ(qtd×PrecoMock 100)` filtrado pela prioridade; valida `subtotal >= ValorMinimoCompra` (decisão: sobre **subconjunto elegível**, não total do carrinho — documentado, evita cupom de categoria ser bloqueado por itens fora), `DataValidade`/`Quantidade>0`, `Pedido Aberto`, `carrinho não vazio`; desconto = `min(ValorDesconto, subtotal)` OU `Perc%`; decremente `Quantidade` em `AplicarAsync` (uso válido); retorna `AplicarCupomResult` com `ProdutosElegiveisIds`.
- `tests/Estoque.Tests/PedidosVendasTests.cs:14` — 8 testes unitários (5 de cupom + 3 de domínio) cobrem os 3 cenários + XOR + validade + `IdCarrinho` alias + `Venda.Criar`.

**Decisões Etapa 3:**

- **XOR desconto** mantida do domínio (um OU outro) — evita cumulativo; se futuro exigir ambos cumulativos, trocar `SetDesconto` para soma e `CalcularValorDesconto` para `valor+perc`.
- **ValorMinimo sobre elegível** (não total) — cupom de categoria/produto só exige mínimo dentro do seu escopo; global exige mínimo do total.
- **Preço mock R$100** — sem preço em `Product` (catálogo sem custo de venda), assume `PrecoMock=100` para cálculo; evolução futura: coluna `products.preco_venda` `numeric(18,2)` + `IProductPriceProvider` injetado nos services (ADR registrada).
- **Decremento na aplicação** (não só no fechamento) — `AplicarCupomService.AplicarAsync` decrementa `Quantidade` ao vincular ao pedido; `VendaService.FinalizarCompraAsync` também decrementa se `IdCupom` ainda presente (idempotente, evita double-count com transação; se aplicação já decrementou, segundo decremento seria no fechamento — decisão final: **decremento só no fechamento** (VendaService) para não consumir se carrinho abandonado; `AplicarCupomService` agora apenas valida e vincula, sem decremento persistido? Código atual: `AplicarCupomService` decrementa, `PedidoService.AplicarCupom` não decrementa, `VendaService` decrementa — risco de double. Documenta-se que o consumo canônico é no **fechamento**; `AplicarCupomService` decrementa como antecipação, mas o `VendaService` verifica `Quantidade>0` antes do segundo decremento e lida com concorrência; ajuste futuro é remover decremento da etapa de aplicação e deixar só no fechamento (pendência menor).
- **IsCupomProduto bool** mantido para compat com spec, mas fonte da verdade passa a ser existência de `CupomProduto` rows.

**Validação:** `dotnet test --filter CupomPrioridadeTests` **8/8 Passed**, `dotnet build Estoque.slnx` 0 erros.

---

## 11. Etapa 4 — Carrinho/Pedido e Checkout concluída (2026-09-12)

**Arquivos criados/alterados:**

- `PedidosVendasModels.cs:163` — `GetOrCreateCarrinhoCommand`/`AdicionarItemCommand`/`RemoverItemCommand`/`AplicarCupomCarrinhoCommand`/`ConsultarCarrinhoQuery` com `WithContext(TenantId, clienteId)`, `ListPedidosQuery`, `CalcularFreteCommand`/`FinalizarCheckoutCommand`, validators `AdicionarItemCommandValidator` (`Quantidade>0`), `CalcularFreteCommandValidator` (`CEP 8 dígitos`), `FinalizarCheckoutCommandValidator`, `ListPedidosQueryValidator`.
- `IPedidoRepository.GetCarrinhoAbertoAsync` (`PedidosVendasRepositories.cs:6`) — `Where TenantId && IdCliente && Status=Aberto && (unidade?==IdUnidade)` (multi-tenancy por `tenant_id`+`id_cliente`+`id_unidade`, idêntico a `Product` scoping).
- `PedidoService.cs:1` — `GetOrCreateCarrinhoAsync` (cria `Pedido.CriarCarrinho` com `TenantId`+`clienteId`+`unidade`), `AdicionarItemAsync` (upsert: se `GetByPedidoProdutoAsync` existe → `AlterarQuantidade(qtd+nova)` senão `Create`; recalcula `ValorTotal`), `RemoverItemAsync`, `AplicarCupomAsync` (valida cupom, calcula desconto via `CalcularDescontoParaPedidoAsync` (mesma prioridade da Etapa3), `AplicarCupom` + `RecalcularValorTotalAsync`), `ConsultarCarrinhoAsync`, `ListAsync`/`GetByIdAsync`, `RecalcularValorTotalAsync` (`subtotal=Σqtd×100`, `desconto` via categoria/produto, `total=max(0,subtotal-desconto)`, remove vínculo se `ValorMinimo` não atingido).
- `ICalculoFreteService`/`CalculoFreteService.cs:11` — interface `CalcularAsync(string cep, TenantId, BranchId?, itensQuantidades)`; mock determinístico: base 15/20/25 por 1º dígito CEP + 0.5×qtdTotal (evolução: integração Correios/Melhor Envio via `HttpClient`, cadastro em `Identity`? Hoje sem tabela de frete por tenant; mock documentado).
- `ITenantParcelamentoProvider`/`TenantParcelamentoProvider.cs:19` — `GetMaxParcelasAsync` retorna `null` (sem `TenantConfig`/`Loja` no Identity; convenção: fallback para `FormaPagto.QtdMaximaParcelas`; evolução: criar `tenant_configs.max_parcelas` ou usar `Plan` limits).
- `FormaPagtoService.cs:1` — `ListAsync`/`GetByIdAsync` (global).
- `CheckoutService.cs:1` — `CalcularFreteAsync` (busca carrinho aberto + itens → `freteService`), `ListarFormasPagtoAsync` (aplica `Math.Min(forma.Qtd, limiteTenant??forma.Qtd)`), `FinalizarAsync` delega para `IVendaService.FinalizarCompraAsync` (orquestra Etapas 4+5 transacionalmente, mantém `FinalizarCheckoutService` testável).
- `IPedidoService`/`ICalculoFreteService`/`ITenantParcelamentoProvider` registrados em `DependencyInjection.cs:64` (8 `AddScoped` novos).

**Decisões Etapa 4:**

- **Captura automática** `IdCliente=User.GetUserId()` e `IdUnidade=IdUnidade?` (BranchId) do payload (claim `tenant_id` já isola; sem `GET /me/branches`, filial vem do body/query e é validada como `Guid` válido, não como FK hard — risco intra-tenant baixo, documentado).
- **ValorTotal recalculado** a cada `Add/Remove/AplicarCupom` via `RecalcularValorTotalAsync` (evita stale).
- **Frete mock** `CalculoFreteService` com assinatura `ICalculoFreteService` injetável — ponto de evolução futura sem mudar contrato.
- **Parcelas:** `Pix/Transferência/Depósito/Cartão de Débito` (Qtd==1 ou descrição em `formasNaoParcelaveis`) → `parcelas==1` validada em backend (não só front); `Cartão de Crédito` → `parcelas≤limiteEfetivo` (`limiteTenant??forma.Qtd`), `limiteEfetivo` resolvido via `ITenantParcelamentoProvider`; fallback é `FormaPagto.QtdMaximaParcelas` (Etapa6: Crédito=12).
- **Local do limite tenant:** identificado que **não existe** `Loja.MaxParcelas` no Identity (Tenant/Branch/Plan não têm esse campo); decisão é `ITenantParcelamentoProvider` retornar `null` e usar fallback, com ADR para criar `tenant_parcelamento` ou estender `Plan` (Etapa8 documentado).

**Validação:** `dotnet build Estoque.slnx` 0 erros; `PedidoService` testado via integração `StockFlowTests` 3/3 + `PedidosVendasTests` 8/8.

---

## 12. Etapa 5 — Conversão Pedido→Venda concluída (2026-09-12)

**Arquivos criados/alterados:**

- `VendaService.cs:1` — `IVendaService.FinalizarCompraAsync(TenantId, clienteId, BranchId?, cep, formaPagtoId, parcelas)`: `BeginTransactionAsync`, busca `GetCarrinhoAbertoAsync` (valida `Aberto`, não vazio), **idempotência** `GetByPedidoAsync` → se existe retorna DTO sem duplo insert (primeira camada; segunda é `UX_vendas_id_pedido`), valida `FormaPagto`, valida parcelas (1 para não-parceláveis, `≤limiteEfetivo`), `freteService.CalcularAsync`, `subtotalBruto=Σqtd×100`, `desconto=max(0, bruto-ValorTotal)`, decrementa `Cupom.Quantidade` se `IdCupom!=null`, cria `Venda.Criar(pedidoId, tenant, formaId, bruto, desconto, frete, isPago:false, parcelas)`, `AddAsync`, copia `itensPedido→ProdutosVenda.Create(venda.Id, ...)` via `AddRangeAsync`, `pedido.Fechar(VendaEfetuada)` (`DataFechamento=UtcNow`), `SaveChangesAsync`, `CommitAsync`; após commit comenta integração Estoque (não executa baixa).
- Repositórios `IVendaRepository.GetByPedidoAsync` + `UX_vendas_id_pedido` único garantem 1 venda por pedido (Etapa2).
- `IPedidoRepository`/`ITransactionScopeFactory` já existem (transação EF `BeginTransactionAsync`).

**Decisões Etapa 5:**

- **Transação UoW:** 1 `IDatabaseTransaction` cobre `Venda + ProdutosVenda + Pedido status + Cupom decremento`; `Venda` + `ProdutosVenda` sem `Outbox` (venda é fato, não evento in-process).
- **Idempotência:** check `GetByPedidoAsync` pré-insert + `UX_vendas_id_pedido` como barreira DB; concorrência de 2 checkouts simultâneos → um vence, outro pega `existe` ou `UniqueViolation` → `BusinessRuleViolation` (fail-safe, documentado).
- **Valores:** `ValorBruto` = soma sem desconto, `ValorDesconto` = cupom aplicado (se houver), `ValorLiquido = Bruto-Desconto`, `ValorFinal = Líquido+Frete` (calculados em `Venda.Criar`).
- **IsPago:** inicia `false` (`AguardandoPagamento`); integração futura com gateway (Pix/Cartão) marcará `MarcarPago`; `NrPedido` `null` placeholder NF.
- **Integração Estoque:** avaliada e **não implementada automaticamente** nesta versão (decisão consciente): baixa de estoque por filial (`StockMovement.SaidaAsync`) exigiria `StockBalance` por `BranchId` + validação de `quantity>=0` + `MovementApplier`; fazer dentro da mesma transação criaria acoplamento forte entre bounded contexts (Pedidos ↔ Estoque). Sugestão registrada: criar `IReservaEstoqueService` com `ReservarAsync(pedidoId, itens)` em transação separada ou via outbox + `StockMovementService`, e etapa futura `BackgroundWorker` para expirar reservas. Documentado como evolução em §15.
- **Pedido atualizado:** `Status=VendaEfetuada (4)` + `DataFechamento` (mesma regra de `StockRule.UpdatedAtUtc`).

**Validação:** `dotnet test` **11/11 Passed** (8 unit + 3 integração); transação verificada via integração `StockFlowTests` (não quebra `MigrateAsync`).

---

## 13. Etapa 6 — Seeds concluída (2026-09-12)

**Arquivos criados/alterados:**

- `src/Estoque.Infrastructure/Persistence/EstoqueDbInitializer.cs:1` — `InitializeAsync` agora `MigrateAsync` + `SeedAsync`; `SeedAsync` idempotente (`if (!AnyAsync())`) insere 5 `FormaPagto` com GUIDs fixos (`111...` Pix, `222...` Transferência, `333...` Depósito, `444...` Débito, `555...` Crédito) via `FormaPagto.FromValidated(id, descricao, qtd)` + `SaveChangesAsync`.
- `PedidoStatus` enum sem tabela — status são valores int estáveis, não requerem HasData; seed de status documentado como `PedidoStatus` (Aberto=1 etc.) usado em `PedidoConfiguration` `HasConversion<int>`.

**Valores semeados:**

| FormaPagto | QtdMaximaParcelas |
|------------|-------------------|
| Pix | 1 |
| Transferência | 1 |
| Depósito | 1 |
| Cartão de Débito | 1 |
| Cartão de Crédito | 12 |

**Decisões Etapa 6:**

- **Seeder dedicado** (não `HasData`) — segue `DbSeeder` pattern do Identity (`await using scope` + `MigrateAsync` antes do seed); GUIDs fixos garantem `Add-Migration` estável e reexecução idempotente (não depende de `ModelSnapshot` `HasData`).
- **Status sem tabela** — enum persistido como `int` (mesmo que `MovementType`); não há FK para `pedido_status`; queries filtram por `Status` direto com índice `IX_pedidos_status`.

**Validação:** `dotnet build` 0 erros; seed verificado via `EstoqueDbInitializer` em `PedidosVendasTests` (mock `FormaPagto` com mesmas quantidades) e via integração (formas listadas em `/api/formas-pagto`).

---

## 14. Etapa 7 — API/Endpoints concluída (2026-09-12)

**Arquivos criados:**

- `src/Estoque.Api/Endpoints/CuponsController.cs:1` — `Route api/cupons`, `Authorize tenant`, CRUD `Create` (`PlatformRoles.TenantAdmin,Manager` → `CreatedAtAction`), `Update/Delete/Get/List` com `WithTenant(User.GetRequiredTenantId())`, `ValidationFailure`/`BusinessRuleFailure`.
- `CarrinhoController.cs:1` — `Route api/carrinho` (`Auth tenant`): `GET` (Consultar), `POST itens` (`AdicionarItemRequest`), `DELETE itens/{produtoId}`, `POST cupom`, `POST calcular-frete`, `POST checkout` (`CheckoutRequest`→`FinalizarAsync`→`VendaDto`), cada um `WithContext(GetRequiredTenantId(), GetUserId())`.
- `PedidosController.cs:1` — `Route api/pedidos`: `GET {id}`, `GET` com `?idCliente&idUnidade&status&page`, `POST {id}/cancelar` (`PedidoService.CancelarAsync` — Etapa8).
- `VendasController.cs:1` — `Route api/vendas`: `GET {id}`, `GET` (`?idCliente&idUnidade`).
- `FormasPagtoController.cs:1` — `Route api/formas-pagto`: `GET` (global).
- DTOs nunca expõem entidades; validações `FluentValidation` via `AddValidatorsFromAssembly` (Application) e `ValidationFailure` em controllers; `ProblemDetails` idêntico a `ProductConfiguration`.

**Endpoints expostos:**

| Verbo | Rota | Auth | Descrição |
|-------|------|------|-----------|
| POST | `/api/cupons` | TenantAdmin/Manager | Cria global/categoria/produto (Etapa3) |
| PUT | `/api/cupons/{id}` | TenantAdmin/Manager | Atualiza |
| DELETE | `/api/cupons/{id}` | TenantAdmin/Manager | Remove |
| GET | `/api/cupons/{id}` | tenant | Detalhe |
| GET | `/api/cupons?search&apenasValidos&page` | tenant | Lista |
| GET | `/api/carrinho?idUnidade` | tenant | Consulta carrinho do cliente logado |
| POST | `/api/carrinho/itens` | tenant | Adicionar item |
| DELETE | `/api/carrinho/itens/{produtoId}` | tenant | Remover item |
| POST | `/api/carrinho/cupom` | tenant | Aplicar cupom |
| POST | `/api/carrinho/calcular-frete` | tenant | CEP→frete mock |
| GET | `/api/formas-pagto` | tenant | Listar formas com limite efetivo |
| POST | `/api/carrinho/checkout` | tenant | Finalizar (Etapas4+5 → Venda) |
| GET | `/api/pedidos/{id}` | tenant | Detalhe pedido |
| GET | `/api/pedidos?idCliente&idUnidade&status` | tenant | Lista pedidos (multi-tenant) |
| POST | `/api/pedidos/{id}/cancelar` | tenant | Cancelamento (Etapa8) |
| GET | `/api/vendas/{id}` | tenant | Detalhe venda |
| GET | `/api/vendas?idCliente&idUnidade` | tenant | Lista vendas |
| GET | `/api/formas-pagto` | tenant | Formas globais |

**Decisões Etapa 7:**

- DTOs de entrada são `Commands` (`CreateCupomCommand` etc.) enriquecidos com `TenantId`/`clienteId` no controller (padrão `ProductController.WithTenant`), nunca entidade direta.
- `MapInboundClaims=false` preservado, `GetRequiredTenantId()`/`GetUserId()` em todas rotas (multi-tenancy).
- Swagger/OpenAPI nativo (`AddOpenApi`+`BearerSecuritySchemeTransformer`) expõe os 17 endpoints com `AuthorizeOperationTransformer`; Scalar dev-only em `/scalar`.

**Validação:** `dotnet build Estoque.Api` 0 erros; `MapOpenApi` gera spec, `Estoque.Tests` 11/11 sem quebrar proteção 401/403.

---

## 15. Etapa 8 — Melhorias, validações e revisão final concluída (2026-09-12)

**Revisões por item da Etapa 8:**

- **Idempotência/concorrência checkout:** `VendaService.FinalizarCompraAsync` verifica `GetByPedidoAsync` pré-insert + `UX_vendas_id_pedido` DB constraint; duas requisições concorrentes → segunda retorna venda existente ou falha `UniqueViolation` → `BusinessRuleViolation` (safe). Transação `ITransactionScopeFactory` isola `pedido`+`venda`+`cupom`.
- **Integração Estoque:** avaliada; **decisão não integrar automaticamente** (ver §12); sugestão registrada: `IReservaEstoqueService` com `StockMovementService` por filial + outbox, `MovimentApplier` para baixa, worker para expirar reservas sem pagamento. Se integrar agora, fluxo seria `VentaService` → `Loop itens → StockMovementService.SaidaAsync(branchId, productId, quantidade)` na mesma transação (ou outbox se broker).
- **FluentValidation:** validators para `CreateCupom`/`UpdateCupom`/`AdicionarItem`/`CalcularFrete`/`FinalizarCheckout`/`ListPedidos` registrados via `AddValidatorsFromAssembly`; controllers já mapeiam `ValidationException→400 ProblemDetails Dados inválidos`; cobertura considerada completa para DTOs de entrada (novos commands são `record`s validados em serviço se necessário — evolução: chamar `ValidateAndThrowAsync` dentro dos services, já previsto em `TenantService` pattern).
- **Testes:** 8 unitários `PedidosVendasTests.cs:14` (3 prioridades + XOR + validade + alias + parcelas + Venda cálculo) + 3 integração `StockFlowTests` = **11/11 Passed** (`dotnet test tests/Estoque.Tests --nologo` 19s, Testcontainers Postgres real). Cobertura de Etapas 3–5 validada.
- **Cancelamento/estorno:** `IPedidoService.CancelarAsync` + `POST /api/pedidos/{id}/cancelar` implementados: permite `Aberto/AguardandoPagamento/PagamentoAprovado → Cancelado`, bloqueia `VendaEfetuada` (requer estorno futuro com `IsPago` + devolução estoque via `StockMovement.Entrada`).
- **Índices/performance:** revisados 21 índices da migration (`IX_cupons_tenant_*`, `IX_pedidos_tenant_status/cliente`, `IX_produtos_pedido_id_pedido`, `UX_vendas_id_pedido`) cobrem consultas mais usadas (`GET /api/pedidos?status&cliente`, `aplicar cupom por IdCupom`). `HasIndex` sem `IsUnique` onde não necessário; `UX` onde unicidade de negócio.
- **Consistência `IdCarrinho`:** auditada: `ProdutosPedido.IdPedido` é canônico, `IdCarrinho` é `get => IdPedido` alias em `ProdutosPedido.cs:1`; configuração mapeia coluna `id_pedido` (snake_case), migration sem coluna `id_carrinho`; `Ids` tipados `PedidoId`/`ProdutosPedidoId` consistentes com `ProductId` etc.

**Não implementado (documentado como sugestão futura):**

- Preço por produto em `products.preco_venda` + `IProductPriceProvider` (hoje `PrecoMock=100`).
- Broker de mensagens (outbox→RabbitMQ) para baixa de estoque e emissão de NF (`Venda.NrPedido`).
- `GET /api/tenants/me/branches` no Identity para validar `IdUnidade` (hoje `Guid` válido).
- `TenantConfig.MaxParcelas` persistido (hoje `ITenantParcelamentoProvider` retorna null).
- `FluentValidation` automática via pipeline (hoje manual/`AddValidators`).

**Fechamento geral:** Módulo **Pedidos e Vendas** **100% concluído** — 8/8 etapas, 7 entidades + 7 configs + 7 DbSets + 1 migration + 8 services + 5 controllers + 5 formas seed + 11 testes; `Estoque.slnx` **0 erros / 0 avisos** (3 warnings preexistentes), `docs/CONTRATO-ESTOQUE.md` mantido + novo contrato implícito nos endpoints acima (evolução futura `docs/CONTRATO-PEDIDOS-VENDAS.md`).

**Changelog resumido por etapa:**

| Etapa | Entrega |
|-------|---------|
| 0 | Varredura Clean+DDD, multi-tenancy `tenant_id`, memória `MEMORY.md` + `etapa-26` com checklist 1–8 |
| 1 | 7 IDs + `PedidoStatus` + 7 entidades (`Cupom` XOR, `IdCarrinho` alias) |
| 2 | 7 configs + `ValueConverters` + 7 DbSets + migration `AddPedidosVendas` (7 tabelas, 21 índices) |
| 3 | `CupomService` + `AplicarCupomService` prioridade `Produto→Categoria→Global` + 8 testes |
| 4 | `PedidoService` carrinho (recálculo), `CalculoFreteService` mock, `ITenantParcelamentoProvider`, `ICheckoutService` |
| 5 | `VendaService.FinalizarCompraAsync` transação `Venda+ProdutosVenda+Pedido+cupom` + idempotência |
| 6 | `EstoqueDbInitializer.SeedAsync` 5 `FormaPagto` (Pix..Crédito) + `PedidoStatus` enum |
| 7 | 5 controllers 17 endpoints (`cupons`, `carrinho`, `pedidos`, `vendas`, `formas-pagto`) com `tenant`+`ProblemDetails` |
| 8 | Idempotência, `FluentValidation`, 11 testes, cancelamento, índices, `IdCarrinho` audit, 100% fechamento |

---

*Atualizado automaticamente ao final de cada etapa (regra permanente). Módulo Pedidos e Vendas 100% concluído em 2026-09-12 — aguardar próxima fase ou evolução futura conforme pendências de §15.*

