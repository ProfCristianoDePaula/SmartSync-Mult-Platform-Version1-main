# Etapa 27 — Pedidos e Vendas — Etapa 1 (Modelagem de domínio)

**Data:** 2026-09-12
**Status:** Feita — entidades + enum + IDs tipados, build 0 erros

## Objetivo

Criar as entidades de domínio do módulo Pedidos e Vendas seguindo fielmente as convenções de Estoque e Tenants (base `Entity<TId>`, IDs tipados, VOs, encapsulamento, `IHasDomainEvents`).

## O que foi entregue

### IDs tipados (7)
`CupomId`, `CupomProdutoId`, `PedidoId`, `ProdutosPedidoId`, `VendaId`, `ProdutosVendaId`, `FormaPagtoId` — `readonly record struct(Guid Value)` com `New()`/`From(Guid)`, espelhando `ProductId.cs:1` (`src/Estoque.Domain/Common/`).

### Enum
`PedidoStatus.cs:1` — `Aberto=1, AguardandoPagamento=2, PagamentoAprovado=3, VendaEfetuada=4, Cancelado=5` + alias `Fechado = VendaEfetuada`.

### Entidades (7, `src/Estoque.Domain/Entities/`)

- **Cupom** — `TenantId`, `Descricao` (≤255), `ValorDesconto`/`PercDesconto` (XOR, ver decisões), `ValorMinimoCompra`, `IdCategoria` (`CategoryId?` nullable → global), `DataValidade`/`DataCriacao` (Utc, não-retroativa), `Quantidade` (int ≥0), `IsCupomProduto`; `IHasDomainEvents`; `Create`/`FromValidated`/`Atualizar`/`DecrementarUso`/`EstaValido`.
- **CupomProduto** — `CupomId` + `ProductId` (N:N, exclusividade por produto).
- **Pedido** — `TenantId` + `DataAbertura` + `IdCliente` (`Guid?`) + `IdCupom` (`CupomId?`) + `IdUnidade` (`BranchId?` nullable) + `PedidoStatus` + `DataFechamento` + `ValorTotal`; `CriarCarrinho`/`AplicarCupom`/`Fechar`/`AlterarStatus`.
- **ProdutosPedido** — `PedidoId` (alias `IdCarrinho` para compat), `ProductId`, `Quantidade` (4 casas).
- **Venda** — `PedidoId` (FK única) + `TenantId` + `DataVenda` + `FormaPagtoId` + `ValorBruto`/`ValorDesconto`/`ValorLiquidoPedido`/`ValorFrete`/`ValorFinal` + `IsPago` + `NrPedido` (`string?` ≤50) + `QuantidadeParcelar`; `Criar` calcula líquido/final.
- **ProdutosVenda** — cópia imutável de `ProdutosPedido` (`FromPedido`).
- **FormaPagto** — global (sem TenantId), `Descricao` (≤100), `QtdMaximaParcelas` (1–36), `EhParcelavel`.

Build `dotnet build Estoque.slnx` — **0 erros / 0 avisos** (3 avisos preexistentes não relacionados).

## Decisões técnicas

- **IdCarrinho → IdPedido:** `ProdutosPedido.IdPedido` é o canônico; `IdCarrinho` mantido como alias `get => IdPedido` (spec usa sinônimo carrinho=pedido aberto). Coluna futura `id_pedido` (snake_case), sem migração legada.
- **Status alias:** `Fechado = VendaEfetuada` cobre ambos os nomes do prompt; persistência como `int`.
- **Desconto XOR:** `Cupom.SetDesconto` exige exatamente um de `ValorDesconto`/`PercDesconto` >0 (não ambos, não zero). Evita cumulatividade ambígua na Etapa 3; ADR para futuro cumulativo.
- **Multi-tenancy:** `Cupom`/`Pedido`/`Venda` ganharam `TenantId` (spec bruto omitia, mas mecanismo da Etapa 0 exige escopo por `tenant_id` claim); `IdUnidade` tipado `BranchId?` nullable; `FormaPagto` global.
- **Precisões:** monetários 2 casas / quantidades 4 casas (`Math.Round`), alinhado a `Money`/`Quantity` do Estoque.
- **Padrão Estoque:** `Create` privado, `FromValidated` para EF, setters privados, `ArgumentException` vs `BusinessRuleViolationException`, `IHasDomainEvents` onde há transação/outbox.

## Arquivos

`Common/CupomId.cs`, `CupomProdutoId.cs`, `PedidoId.cs`, `ProdutosPedidoId.cs`, `VendaId.cs`, `ProdutosVendaId.cs`, `FormaPagtoId.cs`; `Enums/PedidoStatus.cs`; `Entities/Cupom.cs`, `CupomProduto.cs`, `Pedido.cs`, `ProdutosPedido.cs`, `Venda.cs`, `ProdutosVenda.cs`, `FormaPagto.cs`.

## Pendências / Próximos passos

- Etapa 2 — Persistência: `IEntityTypeConfiguration` para as 7 entidades, `DbSet`s em `EstoqueDbContext`, `ValueConverters`, índices (`IdCupom`, `IdCliente`, `Status`, `TenantId`), `numeric(18,2/4)`, filtro multi-tenancy por `TenantId`/`BranchId` (`IdUnidade`), migration e validação sem conflitos.
