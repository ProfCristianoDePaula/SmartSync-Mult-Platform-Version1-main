# Etapa 28 — Pedidos e Vendas — Etapa 2 (Persistência)

**Data:** 2026-09-12
**Status:** Feita — 7 configurations + DbSets + migration sem conflitos

## Objetivo

Implementar a camada de persistência para as 7 entidades da Etapa 1, seguindo o padrão EF Core do Estoque/Tenants (IEntityTypeConfiguration, snake_case, ValueConverters, índices, numeric precision, multi-tenancy).

## O que foi entregue

### ValueConverters & DbContext

- `ValueConverters.cs:1` — 7 conversores novos (`Cupom`, `CupomProduto`, `Pedido`, `ProdutosPedido`, `Venda`, `ProdutosVenda`, `FormaPagto`) com `v => v.Value` / `TId.From(v)`, nullable-safe.
- `EstoqueDbContext.cs:1` — 7 `DbSet` (`Cupons`, `CupomProdutos`, `Pedidos`, `ProdutosPedidos`, `Vendas`, `ProdutosVendas`, `FormasPagto`) + `ApplyConfigurationsFromAssembly`.

### Configurations (7, `Persistence/Configurations/`)

- **Cupom** → `cupons`: `tenant_id`, `descricao` 255, `valor_desconto`/`valor_minimo_compra` `18,2`, `perc_desconto` `5,2`, `id_categoria` (Category?), `data_validade`/`data_criacao`, `quantidade`, `is_cupom_produto`; índices `tenant_id`, `id_categoria`, `data_validade`, `tenant_validade`.
- **CupomProduto** → `cupom_produtos`: `id_cupom`/`id_produto` únicos `UX_cupom_produtos_cupom_produto`, índices isolados.
- **Pedido** → `pedidos`: `tenant_id`, `data_abertura`, `id_cliente` (Guid? sem conversão, FK para Identity), `id_cupom` (Cupom? nullable), `id_unidade` (`Branch?` coluna `id_unidade` — spec `IdUnidade`), `status` `int→PedidoStatus`, `data_fechamento`, `valor_total` `18,2`; índices `tenant_id`, `id_cliente`, `status`, `tenant_status`, `tenant_cliente`, `id_cupom`, `id_unidade`; `Ignore(Itens/DomainEvents)`.
- **ProdutosPedido** → `produtos_pedido`: `id_pedido` (`Pedido`, coluna `id_pedido` resolve `IdCarrinho`), `id_produto` (Product), `quantidade` `18,4`; `UX_pedido_produto` único + índices.
- **Venda** → `vendas`: `id_pedido` único `UX_vendas_id_pedido` (1:1), `tenant_id`, `data_venda`, `id_forma_pagto`, `valor_bruto/desconto/liquido/frete/final` `18,2`, `is_pago`, `nr_pedido` 50 nullable, `quantidade_parcelar`; índices `tenant_id`, `id_forma_pagto`, `data_venda`, `tenant_data_venda`.
- **ProdutosVenda** → `produtos_venda`: `id_venda`, `id_produto`, `quantidade` `18,4`; `UX_venda_produto` único.
- **FormaPagto** → `formas_pagto`: `descricao` 100 `UX_descricao` único, `qtd_maxima_parcelas`; global (sem tenant).

### Migration

- `20260912140738_AddPedidosVendas.cs:1` — `dotnet ef migrations add AddPedidosVendas --project Estoque.Infrastructure --startup-project Estoque.Api` (**Build succeeded / Done**). 7 `CreateTable` + 21 `CreateIndex` (4 únicos) + `Down` com 7 `DropTable`. Validada contra `20260822190930_InitialCreate` sem colisão de nomes/índices; snapshot atualizado (`EstoqueDbContextModelSnapshot.cs`).
- `dotnet ef database update` — sem Postgres local (`Failed to connect to 127.0.0.1:5432`), esperado; aplicação ocorrerá via `EstoqueDbInitializer.InitializeAsync → MigrateAsync()` quando `estoque_postgres` saudável (padrão Etapa 00).

Build `dotnet build Estoque.slnx` — **0 erros / 0 avisos** (3 warnings preexistentes).

## Decisões

- Precisions `18,2` monetário / `18,4` quantidade; `perc_desconto` `5,2`.
- FKs sem `HasOne` físico (mesmo que `Product` → Brand/Model) — validação em serviço + índice único.
- Multi-tenancy: `tenant_id` obrigatório em Cupom/Pedido/Venda com `IX_tenant_id` + `IX_tenant_status/cliente`; sem `HasQueryFilter` global (escopo manual `Where TenantId`); `id_unidade` como `Branch?` (`id_unidade`) com índice.
- `IdCarrinho` → coluna `id_pedido`; alias mantido em domínio.
- `Venda` 1:1 com `Pedido` via índice único.

## Pendências

- Etapa 3 — Cupons: serviço `AplicarCupomService` com prioridade CupomProduto→Categoria→Global, validações `ValorMinimoCompra`/`Validade`/`Quantidade`, testes.
