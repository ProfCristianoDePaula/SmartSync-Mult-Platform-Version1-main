# Índice de Memória do Projeto

> Última atualização: Etapa 34 (Pedidos e Vendas 100% — Etapas 3–8: cupons + carrinho + venda + seeds + API + revisão final, 11/11 testes)

## O que é este diretório

Registro cumulativo de decisões, contextos e pendências de cada etapa do
desenvolvimento. A regra de trabalho é: **antes de iniciar qualquer etapa,
leia este índice e as memórias já existentes** para não repetir nem contradizer
decisões já tomadas. Ao final de cada etapa, crie/atualize a memória da etapa e
este índice.

## Projeto

**Serviço:** Identity & Tenants (primeiro microsserviço da plataforma)
**Papel:** cadastro de Tenants, Filiais (Branches) e toda a
Identidade/Autenticação/Autorização consumida via JWT pelos demais serviços.

## Histórico de Etapas

| Etapa | Arquivo | Status | Resumo |
|-------|---------|--------|--------|
| 00 | `etapa-00-base-da-memoria.md` | Feita | Estrutura `docs/memoria/` criada |
| 01 | `etapa-01-estrutura-e-docker.md` | Feita | Solução .slnx + Clean Architecture + Dockerfile/compose + user-secrets |
| 02 | `etapa-02-dominio-tenant-branch.md` | Feita | DDD Tenant/Branch + VOs validados + EF Core + migration + índice único CNPJ |
| 03 | `etapa-03-identity-roles-jwt.md` | Feita | Identity custom (ApplicationUser/Role) + seed de roles + JWT RS256 + JWKS + login/refresh (implementada, registrada retroativamente) |
| 04 | `etapa-04-regras-unicidade.md` | Feita | Regras de unicidade multi-tenant (índices + validators + describer pt-BR) |
| 05 | `etapa-05-sso-google-facebook.md` | Feita | Login social Google/Facebook exclusivo Client + cookie intermediário + JWT próprio |
| 06 | `etapa-06-confirmacao-email-sms.md` | Feita | Confirmação de e-mail (tokens Identity) + SMS (Twilio trial) + claims/policy de conta ativa |
| 07 | `etapa-07-cache-logging-observabilidade.md` | Feita | Cache (IMemoryCache + Valkey/Redis), Serilog + Correlation ID, health checks, rate limiting em auth, métricas Identity/Prometheus |
| 08 | `etapa-08-docker-final.md` | Feita | Dockerfile multi-stage (non-root), rede `identity-net`, compose base/override, env via `.env`, README com fluxo de subida |
| 09 | `etapa-09-docs-testes-handoff.md` | Feita | OpenAPI nativo 3.1 + Bearer + SwaggerUI, `CONTRATO-IDENTIDADE.md`, suíte de integração (Testcontainers) com cobertura |
| 10 | `etapa-10-credenciais-scalar.md` | Feita | Guia de credenciais externas (`docs/CONFIGURACAO-CREDENCIAIS.md`) + troca do SwaggerUI pelo Scalar (Bearer na UI em /scalar) |
| 11 | `etapa-11-gestao-conta.md` | **Feita (final)** | Gestão completa da conta: `register` (Client autosserviço), `forgot-password` e-mail/SMS, `reset-password`, `change-password`, `logout`, `logout-all`, expirações de token documentadas |
| 12 | `etapa-12-dominio-e-crud-plans.md` | **Feita (final)** | Domínio `Plan` + CRUD exclusivo SuperAdmin (`/api/plans`), soft delete (named query filter), FluentValidation, paginação, 45/45 testes |
| 13 | `etapa-13-crud-tenants.md` | **Feita (final)** | CRUD de Tenants (`/api/tenants`, SuperAdmin) + FK `plan_id` opcional (decisão do usuário), soft delete com índice parcial de CNPJ/e-mail, filtros de listagem, bloqueio de auth de tenants soft-deletados, 64/64 testes |
| 14 | `etapa-14-crud-filiais.md` | **Feita (final)** | CRUD de Filiais aninhado em `/api/tenants/{tenantId}/branches`, **autorização por claim do JWT** (SuperAdmin qualquer tenant; TenantAdmin só o próprio, 403 se rota ≠ claim), soft delete no Branch, garantia de não-vazamento entre tenants, 82/82 testes |
| 15 | `etapa-15-modulos-e-planos-por-modulo.md` | **Feita (final)** | Módulos (`/api/modules`, slug estável/imutável) + planos **por módulo** (`/api/modules/{moduleId}/plans`, nome único por módulo) + vínculo tenant↔module (`/api/tenants/{tenantId}/modules`, **SuperAdmin-only**) com histórico de vigências e troca de plano; `Tenant.PlanId` removido (Etapa 13 substituída por `tenant_modules`), migration defensiva com backfill, 111/111 testes |
| 16 | `etapa-16-fluxo-de-cadastro.md` | **Feita (final)** | Etapa de documentação: `docs/FLUXO-DE-CADASTRO.md` (ordem oficial Module → Plan por Module → Tenant → vínculo → Branch → Usuários), `docs/CONTRATO-IDENTIDADE.md` reescrito (estado atual da API: modules/plans por módulo/vínculos; removidas rotas planas obsoletas), contrato recomendado `GET /api/tenants/me/modules` documentado (pendência Etapa 15 retomada), README com link do fluxo; nenhuma mudança de código |
| 17 | `etapa-17-tenant-cpf-ou-cnpj.md` | **Feita (final)** | **Correção da Etapa 02**: Tenant deixa de ser CNPJ-only e passa a aceitar **CPF ou CNPJ** (`TipoPessoa` + VO `Documento`, reutilizando `Cpf`/`Cnpj`); unicidade global **por número** (`IX_tenants_documento`, independente do tipo); `CreateTenantCommand`/`TenantDto`/`ListTenantsQuery` e filtro `?documento=`; mensagens diferenciadas CPF/CNPJ; migration defensiva com backfill (existente = Juridica); `PtBrIdentityErrorDescriber` inalterado; 115/115 testes |
| 18 | `etapa-18-seed-inicial.md` | **Feita (final)** | **Bootstrap da plataforma**: `DbSeeder` idempotente (Module `core` → Plan Full Access → Tenant SmartSync Platform → Branch → vínculo → SuperAdmin global `sa@smartsync.com.br`); **convenção `null` = "sem limite"** nos limites do plano (`int` → `int?`, migration `MakePlanLimitsNullable`); senha via `SEED_SUPERADMIN_PASSWORD` (default só em Development); `IdentitySeeder` volta a ser só migrations+roles; docs/config migradas de `admin@identity.local` para `sa@smartsync.com.br`; `docs/SEED-INICIAL.md`; 118/118 testes |
| 19 | `etapa-19-correcoes-pos-entrega.md` | **Feita (final)** | **Correções pós-entrega**: e-mail real (Gmail SMTP, log de sucesso, `resend` loga motivo, link de confirmação → API + `GET /confirm-email` com página HTML); **DataProtection persistido** (tokens sobrevivem a restart; `SetApplicationName("Identity")`; correção de ownership do volume `jwtkeys`); **celular por código numérico de 6 dígitos** (`phone_verification_codes`, hash SHA-256, 10 min, vínculo com o número — substitui o token nativo); diagnóstico do trial Twilio (482913 = template fixo, mantido); **`TenantModuleDto` sem `planName`** (plano só por FK `planId`); `SEED_SUPERADMIN_PASSWORD` alinhado; 118/118 testes |
| 20 | `etapa-20-limites-e-consulta-de-modulos.md` | **Feita (final)** | **Fechamento de pendências**: `GET /api/tenants/me/modules` (contrato §12.2 — qualquer role com claim `tenant_id`, slug/plan, sem módulos soft-deletados, 403 sem claim); **limites do plano aplicados** (`MaxBranches` no cadastro de filiais; `MaxUsers` vale para usuários internos — **Client não conta**, register público não é limitado) com regra **soma dos planos ativos** e `null` = sem limite (`IPlanLimitResolver`); **revogação em massa de refresh tokens** por tenant no soft delete (transação, `TokenService.RevokeAllTenantRefreshTokensAsync`); 11 testes novos; execução da suíte bloqueada por política de Controle de Aplicativo do Windows (Smart App Control) sobre o bin de testes — builds 100% OK |
| 21 | `etapa-21-correcao-login-social-recorrente.md` | **Feita (final)** | **Correção do login social recorrente**: causa raiz CONFIRMADA — criação do 1º login **não atômica** (sem transação: `CreateAsync`+`AddToRoleAsync`+`AddLoginAsync` podiam deixar usuário órfão **sem vínculo** em `AspNetUserLogins`, condenando o retorno a 401 "e-mail já existe") + **401 genérico** indistinguível. Novo `SocialAuthResult`/`SocialAuthError` (resultado tipado), ordem corrigida (`FindByLoginAsync` → criação **atômica** com `AddLoginAsync` → conflito), erros diferenciados (400 request/tenant, 401 conflito de e-mail, 403 sem role Client); **bug latente da Etapa 05** corrigido (Challenge usava provider da rota em minúscula vs. scheme canônico `Google`); **suíte 135/135 rodando** em container Linux via `scripts/run-tests-in-docker.ps1` (contorna Smart App Control/0x800711C7); correção colateral de colisão de nome de módulo (Etapa 20); 7 testes sociais novos; **SSO Google validado end-to-end com credencial real (3 cenários)** |
| 22 | `etapa-22-onboarding-perfil-client.md` | **Feita (final)** | **Onboarding pós-login social**: o Client do 1º login nasce com cadastro incompleto (sem documento). API agora sinaliza isso em 2 pontos — claim **`profile_complete`** no JWT (`ApplicationUser.ProfileComplete` = Document + FullName) e campo **`requiresProfileCompletion`** no `TokenResponse` (login/refresh/callback social) — e oferece **`POST /api/auth/profile`** (exclusivo Client, documento obrigatório/duplicado por tenant → 400, reemite tokens revogando sessões anteriores, claim atualizada); **8 testes novos**; suíte **143/143** no container |
| 23 | `etapa-23-arquitetura-microservico-estoque.md` | **Feita (design)** | **Design do microsserviço de Estoque** (NENHUM código): solução `Estoque.slnx` espelhando o Identity (Clean Architecture + DDD); 5 bounded contexts (Catálogo, Movimentações, Políticas, AutoCompra, Integração); agregados `Product/Brand/Model/Category/Supplier/StockMovement/StockBalance/StockRule/Lot/OutletItem/PurchaseSuggestion/XmlImport/Alert`; produto é do TENANT (sem cópia por filial), saldos/movimentações/lotes/regras/outlet POR FILIAL; integração via JWKS + claims (`user_id`/`tenant_id`/`role`) + gate de módulo slug `estoque` (`GET /api/tenants/me/modules` com cache) — Identity intocado; validação de filial via HTTP com cache (limitação v1 documentada: falta `GET /api/tenants/me/branches` para roles operacionais); eventos de domínio in-process + **outbox** (sem broker); 4 BackgroundServices nativos (validade, reposição/AutoCompra, XML, outbox); Postgres próprio `estoque_postgres`; repositórios explícitos (evolução consciente sobre service→DbContext); exceções ProblemDetails + IExceptionHandler global novo; validação em 3 camadas |
| 24 | `etapa-24-implementacao-microservico-estoque.md` | **Feita (final)** | **Estoque IMPLEMENTADO**: `Estoque.slnx` com Domain/Application/Infrastructure/Api + Tests compilando **0 erros/0 avisos**; 13 entidades + VOs + IDs tipados; `MovementApplier` (transferência par espelhado atômica, custo médio) e `ReplenishmentPolicy`; EF Core c/ `ValueConverters` nullable-safe, soft delete filter "Active", índices únicos por tenant, CHECK saldo≥0; JWT RS256 validado por **JWKS remoto do Identity** (`JwksKeyStore` hosted) + policies `tenant` e `module-estoque` (gate fail-closed) + `MapInboundClaims=false`; 13 controllers REST; **outbox** + 4 BackgroundServices (validade/reposição/XML NF-e/outbox); migration `InitialCreate` gerada via container; Docker (`Dockerfile.estoque`, compose :8081 rede identity-net) + `.env.example`; suíte Testcontainers **3/3** (`run-tests-in-docker.estoque.ps1`) cobrindo health, 401, fluxo E2E (SKU duplicado→400, saída>saldo→400, custo médio, transferência, Seller 403) |
| 25 | `etapa-25-finalizacao-planejamento.md` | **Feita (final)** | **Planejamento ENCERRADO**: contrato `CONTRATO-ESTOQUE.md` publicado (fonte da verdade — 14 contextos atendidos: produtos/marcas/modelos/categorias/fornecedores + endereços/contatos VOs + movimentações + regras + validade + outlet + AutoCompra + importação XML + alertas); matriz de rastreabilidade 14/14 ✅; `Estoque.slnx` 0 erros/0 avisos e 3/3 testes verificados |
| 26 | `etapa-26-planejamento-pedidos-vendas.md` + `docs/pedidos-vendas/MEMORY.md` | **Feita (planejamento)** | **Pedidos e Vendas — Etapa 0**: varredura completa (Clean+DDD net10, EF Core 10 + Npgsql, `Entity<TId>`+IDs tipados, `HasQueryFilter("Active")`+índices parciais, `ValueConverters`, `FluentValidation`+`ProblemDetails`, `IUnitOfWork`/Outbox, JWKS+`tenant`/`module-estoque`); convenções de **Estoque** (13 entidades, catálogo por tenant, saldos por filial) e **Tenants** (Documento owned, soft delete parcial, `TenantModule`); multi-tenancy por claim `tenant_id`+escopo manual por `TenantId`/`BranchId` (SuperAdmin 403); memória publicada com checklist Etapas 1–8 `[ ] pendente` |
| 27 | `etapa-27-pedidos-vendas-etapa-01.md` | **Feita** | **Pedidos e Vendas Etapa 1 — Modelagem**: 7 IDs tipados (`CupomId`…`FormaPagtoId`) + enum `PedidoStatus` (5 valores + alias `Fechado`) + 7 entidades (`Cupom`, `CupomProduto`, `Pedido`, `ProdutosPedido`, `Venda`, `ProdutosVenda`, `FormaPagto`) com `TenantId`/`BranchId`, `IHasDomainEvents`, `Create`/`FromValidated`; decisões `IdCarrinho→IdPedido` (alias), desconto XOR, `FormaPagto` global; build `Estoque.slnx` 0 erros |
| 28 | `etapa-28-pedidos-vendas-etapa-02.md` | **Feita** | **Pedidos e Vendas Etapa 2 — Persistência**: `ValueConverters` estendidos (7 IDs) + 7 `IEntityTypeConfiguration` (`cupons`, `cupom_produtos`, `pedidos` com `id_unidade`=`Branch`, `produtos_pedido` `18,4`, `vendas` 1:1 pedido, `produtos_venda`, `formas_pagto` global) com `numeric(18,2/4)`, índices `IX_tenant_*`/`UX_*` únicos; `EstoqueDbContext` + 7 `DbSet`; migration `20260912140738_AddPedidosVendas` (7 tabelas, 21 índices) sem conflitos, pronta para `MigrateAsync()` |
| 29 | `etapa-29-pedidos-vendas-etapa-03.md` | **Feita** | **Cupons Etapa 3**: `CupomService` CRUD + `AplicarCupomService` prioridade `Produto→Categoria→Global` (ValorMinimo sobre elegível, Validade/Quantidade, XOR), mock `Preco 100`, 8 testes unitários |
| 30 | `etapa-30-pedidos-vendas-etapa-04.md` | **Feita** | **Carrinho/Checkout Etapa 4**: `PedidoService` carrinho Aberto (`TenantId`+`clienteId`+`unidade`) add/remove + recálculo `ValorTotal`, `ICalculoFreteService` mock (15/20/25+0.5×qtd), `ITenantParcelamentoProvider` fallback, `ICheckoutService` (frete/forma/parcelas 1 vs ≤limite) |
| 31 | `etapa-31-pedidos-vendas-etapa-05.md` | **Feita** | **Venda Etapa 5**: `VendaService.FinalizarCompraAsync` em `BeginTransaction` (cria `Venda` `Bruto/Desconto/Líquido/Frete/Final`+`IsPago`, copia `ProdutosPedido→ProdutosVenda`, `Pedido→VendaEfetuada+DataFechamento`, decrementa cupom, idempotência `UX_vendas_id_pedido`), estoque baixa como evolução futura |
| 32 | `etapa-32-pedidos-vendas-etapa-06.md` | **Feita** | **Seeds Etapa 6**: `EstoqueDbInitializer.SeedAsync` idempotente 5 `FormaPagto` GUIDs fixos (Pix/Trans/Dep/Déb 1, Crédito 12) + `PedidoStatus` enum |
| 33 | `etapa-33-pedidos-vendas-etapa-07.md` | **Feita** | **API Etapa 7**: 5 controllers 17 endpoints (`cupons` CRUD, `carrinho` itens/cupom/frete/checkout, `pedidos` lista/cancelar, `vendas`, `formas-pagto`) DTOs, `FluentValidation→ProblemDetails`, `tenant`+`module-estoque`, Scalar |
| 34 | `etapa-34-pedidos-vendas-etapa-08.md` | **Feita (100%)** | **Revisão final Etapa 8**: idempotência `UX+check` + tx, `FluentValidation` completo, 11/11 testes (8 unit + 3 integração Testcontainers), cancelamento `POST /pedidos/{id}/cancelar`, estoque integração proposta, índices auditados, `IdCarrinho` alias, **módulo 100% fechamento** |

## Decisões Estruturantes

- Identificação de branch (estratégia de multi-tenant/multi-filial) — pendente de definição
- Pacote de Identity/Acesso vs. OpenIddict (Open Source, free tier viável) — pendente de definição
- Provedor de SMS para validação de celular — **definido (Etapa 06)**: ISmsSender com Twilio trial + fallback de log em dev (limitação documentada)
- Política de expiração/renovação de tokens — definida na Etapa 03 (rotação de refresh)
- Confirmação de e-mail: **tokens nativos do Identity** (Etapa 06)
- Bloqueio de conta: **e-mail confirmado obrigatório**; celular informativo (Etapa 06)
- Observabilidade: **Serilog + Correlation ID**, health checks no Postgres, **rate limit**
  por IP no login/refresh, métricas **OpenTelemetry/Prometheus** (Etapa 07)
- Cache: **IMemoryCache** padrão; **Valkey/Redis** distribuído via `Cache:Mode=redis`
  (Etapa 07)
- Container: **Dockerfile non-root** (`app` UID 1654), **rede `identity-net`**,
  **`.env` fora do git** (só `.env.example` versionado), compose **base=prod** +
  **override=dev** (Etapa 08)
- Documentação/contrato: **OpenAPI v3.1 gerado nativamente** (`/openapi/v1.json`) +
  transformers de Bearer/autorização, **`CONTRATO-IDENTIDADE.md`** na raiz (Etapa 09)
- Testes: **xUnit + Testcontainers Postgres** (`tests/Identity.Tests`, fixture
  compartilhada na collection `"integration"`), implementação de `IEmailSender`/
  `ISmsSender` capturadores — **sem mocks de rede**, Postgres 100% real (Etapa 09)
- UI de documentação: **Scalar** (`Scalar.AspNetCore` 2.16.18, em `/scalar`,
  dev-only) no lugar do SwaggerUI; consome o OpenAPI nativo e habilita
  **Bearer JWT** na UI para testes manuais (Etapa 10)
- Credenciais externas: **NUNCA em `appsettings.json` versionado** — dev via
  `dotnet user-secrets`, prod via env `__`/secret manager; guia completo em
  `docs/CONFIGURACAO-CREDENCIAIS.md` (Etapa 10)
- Gestão de conta (Etapa 11): reset de senha por e-mail (token nativo, 30 min) e
  SMS (código 6 dígitos, 10 min), troca de senha autenticada, logout/logout-all
  com **revogação de refresh tokens no banco**; `register` público de Client
  (tenantId no body) com confirmação por e-mail
- Catálogo de roles: `GET /api/roles` (autenticado) devolve as roles da
  plataforma na ordem canônica (`Identity.Domain.Common.Roles`), lendo o banco
  via `RoleManager`; serviço em `IRoleService`/`RoleService` (namespace
  `RoleCatalog` — evita colisão com o tipo `Roles`)
- Plans (Etapa 12): domínio `Plan` + CRUD `/api/plans` **exclusivo SuperAdmin**;
  **soft delete** com **named query filter** (`HasQueryFilter("Active")`, ignorado
  via `IgnoreQueryFilters` para listar inativos); nome **único entre planos
  ativos** (índice parcial `IX_plans_name_active`); validação com **FluentValidation**
  (commands/queries como DTOs, sem MediatR — consistente com `IAuthService`)
- Tenants (Etapa 13): CRUD `/api/tenants` **exclusivo SuperAdmin**; FK
  **`plan_id` OPCIONAL** no nascimento (decisão do usuário; atribuível depois);
  soft delete com named query filter e **índices parciais** de CNPJ/e-mail
  (`WHERE is_active` — soft-deletado libera o valor); unicidade global reusa
  `TenantUniquenessValidator` (Etapa 04); login/refresh de usuários de tenant
  soft-deletado bloqueado (`401`)
- Filiais (Etapa 14): CRUD aninhado `/api/tenants/{tenantId}/branches`, roles
  SuperAdmin+TenantAdmin; **autorização por claim `tenant_id` do JWT** — sem claim
  (SuperAdmin global) opera em qualquer tenant, com claim a rota **deve** bater
  (senão 403); queries sempre escopadas por TenantId (sem vazamento entre
  tenants); soft delete no Branch com named query filter "Active"
- Módulos/Planos (Etapa 15): **`Module`** com **slug estável e imutável**
  (identificador usado pelos outros microsserviços); `Plan` pertence a **um**
  módulo (nome único **por módulo** entre ativos); **`Tenant.PlanId` removido** e
  substituído pelo vínculo **`TenantModule`** (`tenant_modules`) — no máximo UM
  vínculo ATIVO por (tenant, module), **troca de plano inativa a vigência atual e
  abre uma nova** (histórico p/ billing), desvincular preserva o histórico;
  `PlansController` aninhado em `/api/modules/{moduleId}/plans`; vínculos em
  `/api/tenants/{tenantId}/modules` **exclusivo SuperAdmin** (decisão do usuário;
  TenantAdmin não opera contratação); nomes de module/plan sempre resolvidos via
  `IgnoreQueryFilters(["Active"])`
- Tenant CPF/CNPJ (Etapa 17 — **correção da Etapa 02**): `Tenant.Cnpj` →
  `Tenant.Documento` (`TipoPessoa` Fisica/Juridica + número); VO `Documento`
  reutiliza `Cpf`/`Cnpj`; **unicidade global pelo NÚMERO** (`IX_tenants_documento`,
  único parcial `WHERE is_active`, sem o tipo — independente de CPF/CNPJ);
  `CreateTenantCommand`/`TenantDto`/`ListTenantsQuery` e filtro `?documento=`;
  mensagens diferenciadas (CPF vs CNPJ); migration defensiva com backfill
  (existente = `Juridica`, sem default 0 inválido gerado pelo `dotnet ef`);
  documento imutável no PUT; `PtBrIdentityErrorDescriber` sem mudanças
- Seed inicial (Etapa 18): `DbSeeder` (`Seed/DbSeeder.cs`, idempotente, roda na
  inicialização em escopo próprio) cria Module `core` → Plan Full Access
  (R$ 0,00, limites `null`) → Tenant SmartSync Platform (pessoa física, CPF
  `295.584.478-03`) → Branch → vínculo `TenantModule` ativo → SuperAdmin global
  `sa@smartsync.com.br` (`TenantId` nulo, JWT sem `tenant_id`); **convenção
  `null` = "sem limite"** nos limites do plano (`int?`, migration
  `MakePlanLimitsNullable`); senha via **`SEED_SUPERADMIN_PASSWORD`** (default
  apenas em Development; fora de Development sem a variável o usuário não é
  criado); `IdentitySeeder` = migrations + roles; e-mail de bootstrap fixo
  `sa@smartsync.com.br` (substituiu `admin@identity.local` em compose/.env/docs)
- Correções pós-entrega (Etapa 19): **e-mail** com log de sucesso no
  `SmtpEmailSender` e `resend-confirmation-email` logando o motivo quando nada
  é enviado; **link de confirmação aponta para a API**
  (`/api/auth/confirm-email`) com novo **GET** devolvendo página HTML (o POST
  permanece); **DataProtection persistido** em `DataProtection:KeyPath`
  (volume), `SetApplicationName("Identity")`, com correção do **ownership do
  volume `jwtkeys`** (root→app) — tokens passaram a sobreviver a restarts;
  **celular por código numérico de 6 dígitos** (`phone_verification_codes`,
  hash SHA-256, 10 min, invalidação de anteriores, vínculo com o número)
  substituindo o token nativo do Identity; trial Twilio mantido (template fixo
  `sms_2fa`; código real só após upgrade); **`TenantModuleDto` sem `planName`**
  (plano apenas por FK `planId`); `SEED_SUPERADMIN_PASSWORD` alinhado à senha
  real do SuperAdmin

## Possível Evolução Futura (não implementada)

- **Passkeys / WebAuthn**: o ASP.NET Core Identity no .NET 10 ganhou suporte
  nativo a passkeys. Não implementado nesta etapa (decisão consciente). Avaliar
  como evolução futura do fluxo de autenticação.
- **OAuth2/OIDC / OpenIddict**: hoje a autorização é JWT próprio (RS256 + JWKS)
  validado pela rede interna. OpenIddict já foi considerado (Etapa 00) como
  alternativa para expor a identidade como provedor OIDC. Decisão futura.

## Pendências que dependem do usuário (Etapa 11)

- **Estratégia de revogação de refresh tokens** — **confirmada** (Etapa 11):
  armazenamento em banco (`refresh_tokens`, hash SHA-256) + revogação por valor
  no `logout` e em massa no `logout-all`/`change-password`/`reset-password`.
  **Sem denylist de JWT em memória/Redis** (access token sobrevive até os 15 min).
- ~~**Lifespan do token de reset por e-mail**~~ — **decidido** (Etapa 11):
  **30 min** via `DataProtectionTokenProviderOptions.TokenLifespan` (também vale
  para o token de confirmação de e-mail, mesmo provider).
- **Cadastro de roles internas** (Manager/Seller/Delivery/TenantAdmin): o
  **catálogo** `GET /api/roles` existe; falta apenas o endpoint de **criação**
  de roles/usuários internos (futuros endpoints de admin).
- **Credenciais externas a criar/configurar** (guia: `docs/CONFIGURACAO-CREDENCIAIS.md`):
  - Google Cloud OAuth Client ID/Secret (Web app, redirect `signin-google`);
  - Facebook for Developers App ID/Secret (Login, redirect `signin-facebook`);
  - Twilio trial (Account SID/Auth Token/número de origem + número verificado);
  - Gmail pessoal (**decidido**): verificação em 2 etapas + **Senha de App**.
- **Migração futura**: se a conta de e-mail for workspace, fluxo muda para OAuth2
  (XOAUTH2) no `SmtpEmailSender` — documentado, sem implementação.
- **Teste end-to-end real** (SSO Google/Facebook, SMS, e-mail) pendente até as
  credenciais existirem em dev. (Etapa 21 corrigiu a lógica do login social
  recorrente; só o teste pelo navegador continua dependendo das credenciais.)

## Pendências registradas na Etapa 13

- ~~**Sessões/refresh tokens de usuários de um tenant soft-deletado/editado**~~
  (tarefa 4 da Etapa 13) — **resolvido (Etapa 20)**: o soft delete do tenant
  agora revoga em massa os refresh tokens ativos dos usuários do tenant
  (transação, `TokenService.RevokeAllTenantRefreshTokensAsync`); o login/refresh
  já era bloqueado no momento da tentativa (`401`). Ainda sem revogação por
  `Status = Inactive/Suspended` (apenas no soft delete).
- ~~**Limites do plano** (`MaxBranches`/`MaxUsers`/`MaxStorageMb`)~~ —
  **aplicados (Etapa 20)** no cadastro de filiais (`MaxBranches`); `MaxUsers`
  vale para **usuários internos** (Client NÃO conta — register público não é
  limitado), com regra de **soma dos planos ativos** e `null` = sem limite.
  **`MaxStorageMb` segue sem aplicação** (não há feature de armazenamento).

## Pendências registradas na Etapa 15

- ~~**Como os outros microsserviços consultam o acesso do tenant a um módulo**~~
  — **resolvido (Etapa 20)**: implementada a opção (a) `GET
  /api/tenants/me/modules` (qualquer role autenticada por claim `tenant_id`;
  retorna módulos ativos com `slug`/`plan`). Consulta direta ao banco continua
  proibida.

## Pendências registradas na Etapa 16

- ~~**`GET /api/tenants/me/modules`** (contrato recomendado documentado em
  `docs/CONTRATO-IDENTIDADE.md` §12)~~ — **implementado (Etapa 20)**: qualquer
  role do tenant autenticada por claim `tenant_id`; retorna módulos ativos com
  `slug`/`plan`.
- ~~**`docs/DEV-CREDENTIALS.md`**~~ — **resolvido** (Etapa 16): recriado com as
  credenciais padrão de dev (SuperAdmin, login, registro por `type` e SSO).
- ~~**Duplicado obsoleto na raiz**~~ (`CONTRATO-IDENTIDADE.md` v1.1/Etapa 11) —
  **resolvido** (Etapa 16): arquivo **removido**; o contrato oficial é o único
  em `docs/CONTRATO-IDENTIDADE.md`.

## Pendências registradas na Etapa 17

- ~~**`GET /api/tenants/me/modules`**~~ — **implementado (Etapa 20)** (ver acima).
- ~~**Limites do plano** (`MaxBranches`/`MaxUsers`/`MaxStorageMb`)~~ — aplicados
  (Etapa 20) em filiais (`MaxBranches`); `MaxUsers` vale para usuários internos
  (Client NÃO conta — register público não é limitado); `MaxStorageMb`
  segue pendente (sem feature de armazenamento).
- ~~**Revogação em massa de refresh tokens por tenant** no soft delete do
  tenant~~ — **implementado (Etapa 20)**.
- **Documento do usuário** (`ApplicationUser.Document`) segue como string livre
  (CPF/CNPJ por role, Etapa 03) — não foi unificado com o VO `Documento` do
  tenant nesta etapa.

## Pendências registradas na Etapa 18

- ~~**`GET /api/tenants/me/modules`**~~ — **implementado (Etapa 20)** (ver acima).
- ~~**Aplicar os limites do plano** (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) no
  cadastro de filiais/usuários~~ — **aplicados (Etapa 20)**, ignorando limites
  nulos (`null` = sem limite); `MaxBranches` no cadastro de filiais; `MaxUsers`
  para usuários internos (Client NÃO conta — register público não é limitado);
  `MaxStorageMb` segue pendente.
- ~~**Revogação em massa de refresh tokens por tenant** no soft delete do
  tenant~~ — **implementado (Etapa 20)**.
- **Documento do usuário** (`ApplicationUser.Document`) segue como string livre
  (CPF/CNPJ por role, Etapa 03) — não foi unificado com o VO `Documento` do
  tenant.
- **Telefone da filial de bootstrap** é placeholder (`docs/SEED-INICIAL.md`
  documenta) — validar com o valor real antes de expor a produção.

## Pendências registradas na Etapa 19

- ~~**Teste de integração** para `send-sms-code`/`confirm-phone`~~ — já coberto
  por `ConfirmationFlowTests.ValidarCelular_CodigoSmsCapturado_PersisteNumero` e
  `ValidarCelular_CodigoIncorreto_Retorna400` (fluxo validado também em execução
  real).
- **Após o upgrade da conta Twilio**: esvaziar `SMS_BODY_TEMPLATE` no `.env` e
  recriar o container para o código real de 6 dígitos ir no SMS.
- **Twilio Verify** registrado como alternativa de OTP que funciona no trial
  (envia código aleatório real; exige `ServiceSid` + número verificado) — não
  implementado.
- Pendências anteriores que **seguem abertas**: `MaxStorageMb` não aplicado (sem
  feature de armazenamento); `ApplicationUser.Document` como string livre;
  revogação para `Status = Inactive/Suspended` (apenas soft delete hoje).

## Pendências registradas na Etapa 20

- ~~**Executar a suíte completa** (`dotnet test tests/Identity.Tests`)~~ —
  **resolvido (Etapa 21)**: o Smart App Control (WDAC, `0x800711C7`) segue
  ativo no Windows (bloqueia assemblies recém-buildados no bin de testes, sem
  desativação via registro/Defender), mas a suíte agora roda **135/135** num
  container Linux via `scripts/run-tests-in-docker.ps1` (monta projeto + socket
  do Docker, `TESTCONTAINERS_HOST_OVERRIDE`, ryuk desabilitado).
- **`MaxStorageMb`**: aplicar quando existir feature de armazenamento.
- **`ApplicationUser.Document`** como string livre (Etapa 03) — não unificado
  com o VO `Documento` do tenant.

## Pendências registradas na Etapa 21

- ~~**Teste end-to-end do SSO Google (navegador)**~~ — **concluído**: credenciais
  Google criadas no console (app 519661235668) e os 3 cenários validados com
  conta real: 1º login cria Client + grava vínculo em `AspNetUserLogins`
  (**200**); login recorrente reutiliza sem duplicar (**200**, 1 usuário/1
  vínculo); e-mail existente sem vínculo → **401** `EmailConflict`. **Facebook**
  segue sem credenciais (console Meta).
- **Smart App Control escalou** e passou a bloquear **também os bins nativos**
  (por conteúdo) após o build no container — a API nativa não sobe mais neste
  Windows. **Como rodar a API localmente:** container `identity_api` via
  `docker compose up -d --build api`, com `OAuth__Google__*` injetados via `.env`
  (mapeados no `docker-compose.yml`; container não enxerga user-secrets). Estado
  atual: container rodando, health OK e `start` do Google validado (302).
  

## Pendências registradas na Etapa 23 (design do Estoque)

- **`GET /api/tenants/me/branches` no Identity** — adição futura (não alteração
  de comportamento existente) para o Estoque validar posse de filial para roles
  operacionais (Seller/Delivery/Manager). Enquanto não existir, o Estoque
  escopa toda escrita/leitura por `tenant_id` e aceita `branchId` validado por
  formato + cache quando a role permite (risco residual intra-tenant baixo,
  documentado).
- **Issuer/Audience canônicos** (`JWT_ISSUER`, `JWT_AUDIENCE`) devem ser
  definidos uma única vez via env compartilhada entre Identity e Estoque
  (corrige inconsistência appsettings vs compose apontada na auditoria).
- **Broker de mensagens** (RabbitMQ/MassTransit): hoje o Estoque usa outbox +
  polling; migração para broker é evolução futura.
- **SuperAdmin sem acesso ao Estoque v1** (403 sem claim `tenant_id`) — reavaliar
  quando existir administração cross-tenant.

## Como adicionar uma nova etapa

1. Crie `etapa-XX-nome-da-etapa.md` (numeração sequencial `01`, `02`, ...).
2. Adicione uma linha na tabela `Histórico de Etapas` deste índice.
3. Atualize `Última atualização` no topo deste arquivo.