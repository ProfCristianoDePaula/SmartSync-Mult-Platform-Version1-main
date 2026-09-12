# Etapa 26 — Planejamento Pedidos e Vendas (Etapa 0 do módulo)

**Data:** 2026-09-12
**Status:** Feita (planejamento) — varredura + memória criada, aguardando autorização para Etapa 1
**Origem:** Prompt “Etapa 0 — Persona, Contextualização e Plano Mestre” do módulo Pedidos e Vendas

## Objetivo

Assumir a persona de especialista sênior em E-commerce + ASP.NET Core 10 e, **sem escrever código**, fazer varredura completa do workspace para identificar arquitetura, convenções de Estoque/Tenants, multi-tenancy e mecanismo de memória, e publicar a memória do novo módulo com checklist das 8 etapas.

## O que foi entregue

### 1. Varredura

Leitura de `Estoque.slnx`/`Identity.slnx`, `src/Estoque.Domain` (13 entidades + `Entity<TId>` + IDs tipados + VOs), `src/Estoque.Infrastructure` (DbContext + 14 `IEntityTypeConfiguration` + `ValueConverters` + repositórios + `IUnitOfWork`/`ITransactionScopeFactory`/`Outbox`), `src/Estoque.Api` (`Program.cs` com Serilog/CorrelationId/OpenTelemetry/JWKS/policies `tenant`+`module-estoque`, `ClaimsPrincipalExtensions.GetRequiredTenantId()`, `EstoqueControllerBase`), `src/Identity.Domain/Entities/Tenant.cs` + `TenantConfiguration.cs` (owned `Documento`), `docker-compose.yml` (rede `identity-net`, `estoque_postgres` :8081), `docs/CONTRATO-ESTOQUE.md`, `docs/memoria/INDEX.md` + Etapas 23–25 e VOs/configurations de referência (`Product`, `Category`, `Brand`, `StockMovement`).

### 2. Síntese registrada

Conteúdo completo em `docs/pedidos-vendas/MEMORY.md` (e espelhado aqui): arquitetura Clean+DDD, ausência de MediatR/AutoMapper, uso de `FluentValidation` + `ProblemDetails` via `EstoqueControllerBase`, `Entity<TId>` + `readonly record struct` IDs, VOs com `Create`/`FromValidated`, `ValueConverters` nullable-safe, naming snake_case, soft delete `HasQueryFilter("Active")` + índices parciais `UX_*_active` (`"is_active"`), `numeric(18,4)`/`(18,2)`, multi-tenancy por claim `tenant_id` (JWT RS256 validado por JWKS, `MapInboundClaims=false`, escopo manual por `TenantId`/`BranchId` sem filtro global, `module-estoque` fail-closed), e histórico de decisões estruturantes das Etapas 00–25.

### 3. Memória

- Criado `docs/pedidos-vendas/MEMORY.md` (requisitado pelo prompt) com §1–7 + checklist `Etapa 1–8` todas `[ ] pendente`.
- Este arquivo `etapa-26-planejamento-pedidos-vendas.md` como espelho canônico no padrão `docs/memoria/`.
- `docs/memoria/INDEX.md` atualizado (linha 26, `Última atualização`).

## Decisões de planejamento

- **Persona mantida** em toda a interação futura, conforme regra permanente.
- **Padrão de memória:** existe `docs/memoria/` — mantido como canônico; `docs/pedidos-vendas/MEMORY.md` criado em adição para satisfazer o fallback do prompt. Ambos serão atualizados ao final de cada etapa; `INDEX.md` é a porta de entrada.
- **Solução para Pedidos e Vendas:** agregar ao `Estoque.slnx`/Postgres `estoque` (mesmo microsserviço), sem nova solution, seguindo o gate `tenant`+`module-estoque` já existente. Se o usuário optar por novo microsserviço isolado, documentar como ADR na Etapa 1.
- **Multi-tenancy a respeitar:** claim `tenant_id` (`JwtClaims.TenantId`) + `User.GetRequiredTenantId()` + escopo manual por `TenantId`/`IdUnidade` (BranchId); sem `HasQueryFilter` por tenant; SuperAdmin sem claim → 403 na v1.

## Validação

- `docs/pedidos-vendas/MEMORY.md` existe e contém checklist 1–8 pendente + § multi-tenancy.
- `docs/memoria/etapa-26-planejamento-pedidos-vendas.md` criado.
- `docs/memoria/INDEX.md` atualizado.

## Pendências / Próximos passos

- Aguardar autorização do usuário para **Etapa 1 — Modelagem de domínio** (entidades Cupom/CupomProduto/Pedido/ProdutosPedido/Venda/ProdutosVenda/FormaPagto + enum StatusPedido, com decisão sobre `IdCarrinho`→`IdPedido`).
- Etapa 1 deve seguir fielmente `Entity<TId>`, IDs tipados, VOs, e documentar a renomeação no `MEMORY.md`.
