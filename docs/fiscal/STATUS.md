# STATUS — Módulo Fiscal

## Etapa atual

- Etapa 1 — diagnóstico do sistema existente: **concluída** (2026-09-19). Somente documentação; nenhum comportamento alterado.

## Evidências

- HEAD local `3c9a52dfaf2463f51b3e8f7774654725ae5f952e` confirmado via `git rev-parse HEAD` (idêntico ao commit de referência).
- `docs/fiscal/material-base.md` **recebido em 19/09/2026** (conteúdo colado pelo usuário, conversa original 19/09/2026; didático, exemplos não compilados). `AGENTS.md` segue ausente na raiz.
- Mapeamento com caminhos reais em `DIAGNOSTICO.md` §§1–2 (soluções net10.0, EF Core 10.0.10 + Npgsql 10.0.3, Identity + JWT/JWKS, Tenant/Branch/Address, ApplicationUser, Product por tenant, Pedido/Venda/FormaPagto, NfeXmlParser importador, outbox + workers, IdentityApiClient com HttpContext, sem frontend no repo).
- Baseline: `dotnet build Identity.slnx` 0 erros / 2 avisos NU1903; `dotnet build Estoque.slnx` 0 erros / 3 avisos (NU1903 + CS8620 `ProductConfiguration.cs:40`); `docker compose ps` 5/5 healthy; `/api/health` 200 em `:8080` e `:18081`.

## Decisões

- Ver `DECISOES.md` (D1–D8). Destaques: preservar PostgreSQL/Identity/JWT e fronteiras; Fiscal como bounded context novo sem FK cross-DB; Branch sem documento próprio exige `EstabelecimentoEmitente`; IBGE por migração + backfill com revisão; `NfeXmlParser` preservado, contratos de saída novos.

## Arquivos alterados

- `docs/fiscal/DIAGNOSTICO.md` (novo)
- `docs/fiscal/STATUS.md` (este, atualizado com respostas P1/P2)
- `docs/fiscal/FONTES.md` (novo)
- `docs/fiscal/DECISOES.md` (novo)
- `docs/fiscal/material-base.md` (conteúdo didático colado pelo usuário em 19/09/2026)
- Código: nenhum.

## Testes executados

- Builds acima + health checks Docker. Suítes xUnit não re-executadas nesta etapa sem código.

## Falhas anteriores ao trabalho

- Avisos preexistentes: NU1903 `SSH.NET 2025.1.0` (Identity.Tests + Estoque.Tests) e CS8620 `Barcode` nullable em `ProductConfiguration.cs:40`. Sem relação com fiscal; registrados para não confundir com regressão.
- Divergência doc-vs-código resolvida no diagnóstico: `CONTRATO-ESTOQUE.md` fala em `XmlImport (NF-e)` como integração, mas o código comprova que é só importação de itens (`NfeXmlParser` + `XmlImportWorker`), sem emissão/validação/assinatura.

## Pendências

- P1 — **resolvida em 19/09/2026**: material didático recebido e gravado em `docs/fiscal/material-base.md` (didático, exemplos não compilados; confrontar sem replicar literalmente).
- P2 — **resolvida em 19/09/2026**: usuário confirmou **sem frontend, somente backend** neste projeto. Etapas 4/10 entregam APIs + contratos completos; nenhuma tela será declarada implementada.
- P3: fontes oficiais da Etapa 3 ainda por inspecionar na implementação (URLs de documentação em `FONTES.md`; endpoints de emissão não gravados).
- P4: UFs/municípios reais das unidades além de `Jaú/SP` e `São Paulo/SP` (sem IBGE hoje).
- P5: preço real por item (`PrecoMock=100` em `VendaService.cs:90`) antes de prévia/homologação.

## Próximo passo

- Etapa 2 — modelo fiscal, permissões e persistência (pronta para iniciar quando o usuário enviar o prompt da Etapa 2). Escopo: estabelecimento emitente, perfil fiscal com vigência, preferência tenant→unidade, credencial por referência, catálogo, documento + snapshot, eventos/tentativas/sequência/idempotência/outbox/auditoria; migrações aditivas só em banco local descartável.
