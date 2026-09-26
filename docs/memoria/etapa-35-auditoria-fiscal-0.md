# Etapa 35 — Fiscal-0: Auditoria e ADR (sem código de produção)

> Mapeamento do roteiro: Fiscal-0 = Etapa 35. Data-base 19/09/2026.

## Objetivo

Auditar o repositório no HEAD `3c9a52d` (idêntico ao commit de referência), decidir o desenho do módulo Fiscal (NF-e 55, NFC-e 65, NFS-e) e registrar perguntas — sem alterar `src/` ou `tests/`.

## Leitura feita

`AGENTS.md` (criado nesta etapa com REGRAS FIXAS), `INDEX.md`, `CONTRATO-IDENTIDADE.md`, `FLUXO-DE-CADASTRO.md`, `CONTRATO-ESTOQUE.md`, `docs/pedidos-vendas/MEMORY.md`, memórias 00–34, `docs/fiscal/` pré-existente (`DIAGNOSTICO.md`, `STATUS.md`, `FONTES.md`, `DECISOES.md`, `material-base.md`) e código: `Tenant`, `Branch`, `TenantModule`, `ApplicationUser`, `Product/Brand/Model/Category`, `Pedido`, `ProdutosPedido`, `Venda`, `ProdutosVenda`, `FormaPagto`, outbox (`OutboxMessage/Service/DispatcherWorker`), `JwksKeyStore`, policies `tenant`/`module-estoque`, VOs `Cnpj/Cpf/Documento/Address`.

## Entregas

- `AGENTS.md` (novo): REGRAS FIXAS R1–R15 + observações locais (porta efetiva do Estoque `18081`; `material-base.md` didático subordinado às premissas P1–P7).
- `docs/fiscal/AUDITORIA.md` (novo): matriz dado fiscal × onde existe × falta (emitente, destinatário, produto/serviço, venda/pagamento, perguntas diretas: CNPJ alfanumérico **não**, `branch_id` **não**, admin de unidade **não existe**).
- `spikes/fiscal-bibliotecas/AVALIACAO.md` (novo, fora das soluções): Unimake.DFe candidata principal condicionada (`20260918.1641.36`, MIT indicado, NFe+NFCe+NFSe, netstandard2.0 compatível com net10 — tudo `verificado=false` até Fiscal-1/9/12); Zeus descartado como base (sem NFS-e + licença a verificar); plano B próprio. Nenhuma chamada a SEFAZ/gov.br.
- `docs/fiscal/ADR-001-arquitetura.md` (novo): microsserviço `Fiscal` próprio (`Fiscal.slnx`, Postgres próprio, JWKS + `module-fiscal`); dados fiscais no Fiscal por IDs + snapshots (sem FK cross-DB, sem colunas novas em Identity/Estoque); Estoque→Fiscal via HTTP com JWT do usuário, worker fiscal com contexto persistido + identidade de serviço; NFS-e nacional + `INfseProvider`; papéis por Branch; P1–P7 aceitas (P2 como estado-alvo: Branch hoje sem CNPJ).
- `docs/fiscal/PERGUNTAS.md` (novo): regimes, UFs/municípios + CNPJ próprio, NFC-e na v1, quem configura/emite, retenção, A1 vs A3/nuvem, auto-emissão, conectores municipais.
- `docs/fiscal/PENDENCIAS.md` (novo): F0-01–F0-11 com `verificado=false` (NTs, endpoints, IBGE, licença/commit da lib, schemas, tPag/prazos/contingência/retenção, preço real).
- `docs/fiscal/STATUS.md` + `FONTES.md`: atualizados? Não nesta etapa (pertencem ao ciclo antigo Etapa 1); o estado Fiscal-0 vive aqui + `AUDITORIA/ADR/PERGUNTAS/PENDENCIAS`. `STATUS.md` será retomado como `docs/fiscal/STATUS.md` na Fiscal-1 se o fluxo antigo for aposentado — registrado como pendência de organização, sem mudar desenho.

## Decisões

- Incorporadas ao ADR-001 (D-ADR1–D-ADR7). Nenhuma divergência das premissas P1–P7 que exija reescrever o roteiro: P2 exige migração (Branch sem CNPJ hoje), porta `8082` a confirmar contra `18081` do Estoque.
- Ambiguidade relevante (R14) que **muda o desenho se respondida diferente**: PERGUNTAS 1–4 (regimes, UFs/CNPJ próprio, NFC-e, concessão por Branch). Por isso, perguntas antes de código — Fiscal-1 (esqueleto) pode começar sem elas; Fiscal-4+ fica condicionada.

## Validação

- `git status --short`: só `AGENTS.md`, `docs/fiscal/AUDITORIA.md`, `docs/fiscal/ADR-001-arquitetura.md`, `docs/fiscal/PERGUNTAS.md`, `docs/fiscal/PENDENCIAS.md`, `spikes/fiscal-bibliotecas/AVALIACAO.md`, `docs/memoria/etapa-35-*` + `INDEX.md`. **Nenhum arquivo de `src/` ou `tests/` alterado** (aceite Fiscal-0).
- Baseline de build do mesmo HEAD (Etapa 1, sem mudanças desde então): `Identity.slnx` 0 erros / 2 avisos NU1903; `Estoque.slnx` 0 erros / 3 avisos (NU1903 + CS8620) — preexistentes, sem relação fiscal. Containers 5/5 healthy na verificação anterior.
- Matriz completa + recomendação justificada presentes (aceite Fiscal-0).

## Pendências

- Responder `PERGUNTAS.md` (mínimo 1–4) antes da Fiscal-4; o restante condiciona Fiscais 6/10/12/14.
- `PENDENCIAS.md` F0-01–F0-11 com `verificado=false` até as etapas indicadas.
- Ler `AUDITORIA.md` + ADR-001 + `PERGUNTAS.md` antes de liberar a Fiscal-1 (Etapa 36).
