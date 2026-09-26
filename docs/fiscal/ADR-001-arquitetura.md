# ADR-001 — Arquitetura do Módulo Fiscal (Fiscal-0 / Etapa 35)

> Status: **proposta para aprovação** (libera a Fiscal-1 somente após leitura deste ADR + `AUDITORIA.md` + `PERGUNTAS.md`). Data-base 19/09/2026.

## Contexto

SmartSync (.NET 10, DDD, PostgreSQL, Docker): Identity (tenants, filiais, JWT RS256/JWKS, `tenant_id` obrigatório nas rotas operacionais) + Estoque (catálogo por tenant, saldos por filial, Pedidos/Vendas). Fiscal greenfield (NF-e 55, NFC-e 65, NFS-e; CT-e/NFC-e fora salvo pedido). Premissas P1–P7 do roteiro; regras R1–R15 (`AGENTS.md`). Auditoria em `AUDITORIA.md`; spike de libs em `spikes/fiscal-bibliotecas/AVALIACAO.md`; material didático em `material-base.md` (não replicar literalmente).

## Decisão

**D-ADR1 — Microsserviço `Fiscal` próprio (`Fiscal.slnx`), como P1.** `Fiscal.Domain/Application/Infrastructure/Api` + `tests/Fiscal.Tests`, mesmos padrões do Estoque (IDs tipados, ValueConverters, `HasQueryFilter("Active")` + índices parciais, FluentValidation→ProblemDetails, Scalar, Serilog+CorrelationId, outbox). Postgres próprio `fiscal_postgres`; JWT via JWKS remoto (`MapInboundClaims=false`); policies `tenant` + `module-fiscal` (slug `fiscal`, fail-closed via `GET /api/tenants/me/modules` com cache). Identity/Estoque só com mudanças **aditivas** (ex.: seed do módulo `fiscal` se aprovado), listadas por etapa.

**D-ADR2 — Dados fiscais vivem no Fiscal, ligados por ID (sem FK cross-DB).** `EmitenteFiscal(tenantId+branchId)`, `ProdutoFiscal(tenantId+produtoId)`, `ClienteFiscal(tenantId+clienteId)`, `ServicoFiscal`, `NaturezaOperacao`, catálogos, `DocumentoFiscal` + snapshots, eventos, sequência, idempotência, outbox, auditoria. Referências a `Tenant/Branch/Product/Pedido/Venda/ApplicationUser` por **IDs + snapshots imutáveis**; consistência por contratos/eventos versionados. Nenhuma coluna fiscal nova em Identity/Estoque (R2).

**D-ADR3 — Estoque aciona o Fiscal por HTTP com o JWT do usuário; worker fiscal nunca reusa JWT de usuário.** Criação de rascunho/solicitação: `POST /api/vendas/{id}/emitir-nota` (aditivo no Estoque) monta o pedido a partir de `Venda/ProdutosVenda/cliente/frete/desconto` e chama o Fiscal repassando o `Authorization` (mesmo padrão do `IdentityApiClient`). Processamento durável (retry/conciliação) usa **fila/outbox interna do Fiscal + contexto persistido** (emitente/ambiente fixados) e identidade de serviço de escopo mínimo — a desenhar na Fiscal-8. Sem bypass de autorização para jobs.

**D-ADR4 — Biblioteca: `Unimake.DFe` como candidata principal, condicionada.** (Spike 19/09/2026: `20260918.1641.36`, MIT indicado, NFe+NFCe+NFSe anunciados, `netstandard2.0` compatível com net10.) Condições até a Fiscal-9: ler `LICENSE` no commit fixado; spike de compilação `net10.0`; teste de isolamento (duas configurações simultâneas, handlers por thumbprint); confirmação do grupo IBSCBS/NT 2025.002 no Portal Nacional. Zeus descartado como base (sem NFS-e nacional + licença a verificar). Plano B: implementação própria por trecho com schemas oficiais versionados.

**D-ADR5 — NFS-e: Padrão Nacional primeiro + `INfseProvider`.** Município sem emissão nacional = status explícito + ponto de extensão, sem centenas de webservices municipais. DPS/RPS conforme contrato vigente; parametrização municipal via cliente dedicado com `Desconhecido` + cache curto em falha.

**D-ADR6 — Papéis (sem `UnitAdmin` novo por ora).** SuperAdmin: catálogo global. TenantAdmin: configura/emite nas filiais do próprio tenant. Manager/Seller: operam **só** com concessão por `Branch` (a criar na Fiscal-4/Etapa 39); sem concessão, somente leitura. Client: nunca configura/emite. SuperAdmin sem `tenant_id` = 403 em rotas operacionais (R5). Produção: flag global + prontidão por emitente (R10).

**D-ADR7 — Premissas aceitas com ajuste mínimo.** P2: emitente = Branch **com dados fiscais próprios a criar** (hoje Branch não tem CNPJ — migração da Fiscal-4). Certificado no tenant (padrão por CNPJ-base) ou filial (override). P3: CPF fora da v1 (422 claro). P4: sem motor tributário (só validação formal). P6: simulador→homologação→produção, produção travada. P7: PostgreSQL. Porta `8082` se livre (Estoque usa `18081` neste repo).

## Alternativas consideradas

| Alternativa | Por que não (trade-off) |
| --- | --- |
| Bounded context Fiscal dentro do Estoque (mesmo DB/transação com `Venda`) | Acoplaria release fiscal ao Estoque; agregaria certificados/mTLS/SOAP ao processo do carrinho; violaria a fronteira que o roteiro e o precedente Identity→Estoque mandam preservar. Ganho de atomicidade local não compensa; idempotência + conciliação cobrem a separação |
| Colunas fiscais em `Product/Branch/ApplicationUser` | Duplicaria cadastros e quebraria R2; snapshots evitam que edição posterior altere XML autorizado |
| Fila via RabbitMQ já na v1 | Infra extra sem necessidade demonstrada; outbox + `FOR UPDATE SKIP LOCKED` bastam até o volume exigir broker |
| Zeus como base | Sem NFS-e nacional anunciada + licença não confirmada; manter como não-escolha documentada |

## Consequências

Fiscal-1 cria esqueleto + `audit_log`; Fiscais 2–3 criam catálogos; Fiscal-4 cria emitente + concessão por Branch + CNPJ alfanumérico se confirmado; Fiscal-5 cofre; Fiscal-6 perfis; Fiscais 7–8 núcleo + pipeline + simulador; Fiscais 9–12 adapters; Fiscal-13 integração Venda→Fiscal; Fiscal-14 hardening + gate. Cada etapa: build 0/0 + Testcontainers no container + memória + `PENDENCIAS.md` para `verificado=false`.
