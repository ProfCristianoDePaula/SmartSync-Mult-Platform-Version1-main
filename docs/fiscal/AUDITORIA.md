# AUDITORIA — Módulo Fiscal (Fiscal-0 / Etapa 35)

> Sem código de produção. HEAD auditado: `3c9a52dfaf2463f51b3e8f7774654725ae5f952e` (`main`, sem divergência do commit de referência). Complementa — não substitui — `docs/fiscal/DIAGNOSTICO.md` (Etapa 1 antiga).
> Convenção: `verificado=false` = sem fonte oficial/código confirmado; ver `PENDENCIAS.md`.

## 0. Leitura feita

`docs/CONTRATO-IDENTIDADE.md`, `docs/FLUXO-DE-CADASTRO.md`, `docs/CONTRATO-ESTOQUE.md`, `docs/pedidos-vendas/MEMORY.md`, memórias `etapa-00`–`etapa-34` (via `INDEX.md`), `docs/fiscal/material-base.md`, `docs/fiscal/DIAGNOSTICO.md`, e código: `Tenant.cs`, `Branch.cs`, `TenantModule.cs`, `ApplicationUser.cs`, `Product.cs`, `Brand.cs`, `Model.cs`, `Category.cs`, `Pedido.cs`, `ProdutosPedido.cs`, `Venda.cs`, `ProdutosVenda.cs`, `FormaPagto.cs`, `OutboxMessage.cs`, `OutboxService.cs`, `OutboxDispatcherWorker.cs`, `JwksKeyStore.cs` (+ `JwksRefreshService`), `IdentityApiClient.cs`, policies `tenant`/`module-estoque`, VOs `Cnpj/Cpf/Documento/Address`, `Roles.cs`, `JwtClaims.cs`, `TokenService.cs`.

## 1. Matriz dado fiscal necessário × onde existe hoje × falta

### 1.1 Emitente

| Dado | Onde existe hoje (entidade/campo) | Falta |
| --- | --- | --- |
| CNPJ do estabelecimento | Só `Tenant.Documento` (`src/Identity.Domain/Entities/Tenant.cs:17`, owned `tipo_pessoa/documento` em `TenantConfiguration.cs:35-59`, imutável). `Branch` = `TenantId+Name+Address+Contact` (`src/Identity.Domain/Entities/Branch.cs:13-16`), **sem documento próprio** | CNPJ por filial, distinção matriz/filial, vínculo N unidades → 1 CNPJ ou 1:1. Sem isso, P2 (emitente = Branch com CNPJ próprio) é estado-alvo, não estado atual |
| IE / IM | **Inexistentes** em `Tenant`, `Branch`, `Supplier` (grep `IE|IM|InscricaoEstadual|InscricaoMunicipal` em `src/` = só falsos positivos de JWT "emitido") | `IE`, `IM`, indicador de contribuinte, vigência por emitente |
| CNAE | Inexistente | `CNAE` por emitente (texto, com vigência) |
| CRT (1/2/3/4) | Inexistente (nenhum campo de regime; `TenantStatus` é `Active/Inactive/Suspended`, não regime) | `CRT` por emitente + regra Simples/MEI/regime especial **não** colapsados numa flag |
| Razão/fantasia do emitente | Indireto: `Tenant.LegalName/TradeName` (`Tenant.cs:15-16`); `Branch.Name` só (`Branch.cs:14`) | Razão/fantasia por estabelecimento + snapshot imutável no documento |
| Endereço + IBGE município | `Address`: `Street,Number,Complement?,District,City (texto),State (UF 2 letras),PostalCode (8)` (`src/Identity.Domain/ValueObjects/Address.cs:9-44`; Estoque idêntico). Colunas `address_*` (`BranchConfiguration.cs:44-55`). Seeds reais: `Jaú/SP` (`DbSeeder.cs:119-127`), `São Paulo/SP` (testes/docs) | `cMun` IBGE em todo endereço fiscal; migração + backfill `City/UF/CEP → IBGE` com revisão humana; nunca usar nome da cidade como chave |
| Telefone / e-mail emitente | `Contact`: `Phone (10/11), SecondaryPhone?, Email` (`Branch.cs:16`, `BranchConfiguration.cs:57-68`) | Suficiente como contato; falta `e-mail` fiscal dedicado se operação exigir (a decidir na Etapa 39) |
| Certificado / CSC | Inexistentes (grep `PFX|certificado|CSC|A1|A3|mTLS` em `src/` = zero fora JWT) | Modelo da Etapa 40 (referência + cofre + metadados sem segredo) |
| Ambiente/série por documento | Inexistentes | `ConfiguracaoDocumento` por (emitente × NFe55/NFCe65/NFSe × homolog/produção), padrão Simulador (Etapa 39) |

### 1.2 Destinatário

| Dado | Onde existe hoje | Falta |
| --- | --- | --- |
| Vínculo cliente↔pedido | `Pedido.IdCliente: Guid?` (`src/Estoque.Domain/Entities/Pedido.cs:18`) = `user_id` do JWT via `WithContext(GetRequiredTenantId(), GetUserId())` (`CarrinhoController.cs:27-92`); `Venda` alcança cliente via `IdPedido` (`Venda.cs:14`) | Nada a mudar no vínculo; **não** criar FK cross-DB Identity↔Estoque |
| Documento / nome | `ApplicationUser`: `TenantId?, FullName, Document? (string livre), ProfileComplete` (`ApplicationUser.cs:11-28`) | Tipo pessoa, validação (CPF/CNPJ), indicador IE + IE, endereço com IBGE, e-mail, `consumidorFinal`. `ProfileComplete` ≠ perfil fiscal completo |
| Endereço destinatário | Inexistente no usuário | Endereço fiscal completo do cliente (perfil vinculado, sem duplicar login) |
| Snapshot no documento | Inexistente (sem documento fiscal) | Cópia imutável de todos os dados do destinatário usados na emissão |

### 1.3 Produto / serviço

| Dado | Onde existe hoje | Falta |
| --- | --- | --- |
| Catálogo | `Product`: `TenantId+Sku+Name+Barcode?+Brand/Model/Category?+UoM` (`Product.cs:18-29`), por tenant, jamais por filial (`:9-12`); `Brand/Model/Category` unicidade por tenant; `Supplier` com CPF/CNPJ por tenant | Nada a duplicar (R2) |
| NCM / CEST / origem | Inexistentes | `NCM` (texto 8), `CEST` quando aplicável, `origem`, como texto com zeros preservados |
| Unidades comercial/tributável + conversão | Só `UnitOfMeasure` enum | `unidadeComercial`, `unidadeTributavel`, fator de conversão |
| GTIN | Só `Barcode?` (EAN) | `GTIN` ou `SEM GTIN` conforme regra |
| CFOP / CST-CS0SN / alíquotas | Inexistentes | `CFOP` por operação (não fixo no produto), `CST/CSOSN` conforme CRT, alíquotas **informadas**, só validação formal na v1 (P4) |
| IBS/CBS (`cClassTrib`/CST) | Inexistente | Classificação informada + tabela de obrigatoriedade versionada (NT 2025.002 v1.51 — `verificado=false`, reconfirmar no Portal Nacional) |
| Serviço (LC 116/NBS/trib. nacional) | Sem catálogo de serviços | Item LC 116, NBS, código de tributação nacional, alíquota ISS, retenções; município de prestação/incidência por regra da operação |
| Snapshot no item | `ProdutosVenda` é cópia de `ProdutosPedido` (`ProdutosVenda.cs:6-12,30-31`, `VendaService` copia itens) — precedente de snapshot comercial | Snapshot **fiscal** (códigos, valores, tributos, configuração efetiva) no `DocumentoFiscal`, imutável após autorização |

### 1.4 Venda / pagamento / frete / desconto / natureza

| Dado | Onde existe hoje | Falta |
| --- | --- | --- |
| Origem fiscal | `Pedido` (`TenantId, IdCliente?, IdUnidade?, Status, ValorTotal`), `ProdutosPedido` (`IdPedido+IdProduto+Quantidade`), `Venda` (`IdPedido+IdFormaPagto+Bruto/Desconto/Líquido/Frete/Final+IsPago+NrPedido+Parcelas`) (`Pedido.cs:16-23`, `Venda.cs:14-27`, `ProdutosVenda.cs:11-13`) | `emitir-nota` por venda sem duplicar fonte da verdade; venda mista → documentos separados; `Venda` sem `BranchId` direto (resolver via `Pedido.IdUnidade`, bloqueando emissão sem estabelecimento) |
| `tPag` | `FormaPagto` global seedada (Pix/Transferência/Depósito/Débito=1, Crédito=12 — `EstoqueDbInitializer.cs:26-35`), `EhParcelavel` (`FormaPagto.cs:52`) | Tabela `FormaPagto → tPag` conferida no manual vigente (`verificado=false`) |
| Frete / desconto | `Venda.ValorFrete/ValorDesconto/ValorFinal`; cálculo atual com `PrecoMock=100` (`VendaService.cs:90`) + frete mock (`CalculoFreteService.cs:11-23`) | Preço real por item + frete/despesas conforme leiaute antes de qualquer homologação |
| Natureza da operação | Inexistente | `NaturezaOperacao` por tenant (venda, devolução etc.), sem prometer devolução/ST/importação sem implementação |

### 1.5 Perguntas diretas do roteiro

| Pergunta | Resposta (código) |
| --- | --- |
| VO `Cnpj` aceita alfanumérico? | **Não.** `Identity` (`Cnpj.cs:41-52`, regex `^\d{14}$` em `:73-74`) e Estoque (`Cnpj.cs:37-48`, regex `:69-70`) normalizam para só dígitos e exigem 14 numéricos + DV mod-11. CNPJ alfanumérico (NT Conjunta 2025.001) exige VO novo/extensão + cronograma confirmado (`verificado=false` → `PENDENCIAS.md`) |
| Claim JWT traz `branch_id`? | **Não.** Claims (`Identity.Domain/Common/JwtClaims.cs:9-22`): `user_id, tenant_id, role, full_name, email, *_confirmed, profile_complete`. Emissão (`TokenService.cs:49-52`) grava `tenant_id` + roles; leitura Estoque (`ClaimsPrincipalExtensions.cs`) só `user_id/tenant_id`. Sem `branch_id` |
| Papel/claim de "admin da unidade"? | **Não existe.** Roles (`Roles.cs:8-13`): `SuperAdmin,TenantAdmin,Manager,Seller,Delivery,Client`. `BranchesController` = SuperAdmin/TenantAdmin; sem associação usuário↔Branch. `Manager` ≠ admin de todas as unidades. Etapa 39/A DR precisam criar concessão por `Branch` (ou leitura-only sem ela) |

## 2. O que falta (consolidado para o ADR)

Emitente com CNPJ/IE/IM/CNAE/CRT/IBGE; perfil fiscal de cliente/endereço; perfil fiscal de produto/serviço + operação sem motor tributário; catálogos UF/autorizador e município/IBGE/NFS-e; certificado/cofre/CSC; documento + snapshot + sequência atômica + chave 44 + idempotência + outbox durável com lease; adapters NF-e/NFC-e/NFS-e; DANFE/DANFCE + armazenamento; integração Venda→Fiscal; hardening + gate de produção. Tudo greenfield integrado — nenhuma emissão iniciada (só `NfeXmlParser` importador de `cProd/xProd/qCom/vUnCom/uCom`, `XmlImportWorker`, `OutboxDispatcherWorker` que só marca `PublishedAtUtc`, `IdentityApiClient` com forward de JWT).

## 3. UFs/municípios/endereços encontrados

`Jaú/SP` (seed), `São Paulo/SP` (testes/docs); UF só como `State` 2 letras, município só `City` texto, **zero IBGE** em `src/`. Escopo inicial sugerido: SP primeiro (a confirmar contra unidades reais) + NFS-e nacional no município da sede; demais localidades como cadastradas-sem-integração até verificação.
