# HOMOLOGAÇÃO — Módulo Fiscal (Fiscal-10)

> O agente NÃO executa chamadas reais. Este passo a passo é MANUAL, com
> empresa e certificado reais em homologação. Produção segue travada (R10).

## Pré-requisitos

1. Emitente cadastrado (`POST /api/fiscal/emitentes`) com CNPJ/IE/endereço+IBGE.
2. Módulo `fiscal` contratado pelo tenant (ver `CADASTRO-DO-MODULO.md`).
3. Certificado A1 válido da empresa (`POST /api/fiscal/certificados`), com
   CNPJ-base igual ao do emitente.
4. UF e endpoints de homologação no catálogo (`GET /api/fiscal/ufs/{uf}/ambientes`).

## Roteiro NF-e (homologação)

1. `POST /api/fiscal/emitentes/{id}/testar-conexao` → `online: true`
   (StatusServico). Se falhar, NÃO prossiga: verifique certificado, rede e
   catálogo antes de qualquer emissão.
2. Confira a prontidão: `GET /api/fiscal/emitentes/{id}/prontidao?tipo=55&ambiente=1`
   (todos os itens `ok: true`).
3. Crie o documento com `Idempotency-Key` e `tipo: 55`, observando os casos
   permitidos em homologação (dados e textos de teste do projeto).
4. Aguarde o worker (5 s) ou chame `POST /api/fiscal/documentos/{id}/consultar`.
5. Interprete `cStat`/`xMotivo`/`protocolo` (tabela `CStatTabela`; HTTP 200 não
   é autorização). Guarde o XML (`GET /{id}/xml`) e o protocolo.
6. Teste cancelamento: `POST /{id}/cancelamento` com justificativa; confirme o
   protocolo do evento. Teste rejeição com dado inválido permitido e confira
   que o status é `Rejeitado` (nunca `Autorizado`).
7. Registre abaixo: UF, ambiente, operações, cStats observados e pendências.

## Roteiro NFS-e (Fiscal-12)

1. Emitente apto no município (`GET /api/fiscal/nfse/municipios/{ibge}/situacao`
   → `NacionalEmissorPublico`). MEI segue as orientações próprias do Emissor
   Nacional.
2. Cadastre tomador (`PUT /api/fiscal/clientes/{id}`) e serviço
   (`PUT /api/fiscal/produtos/{id}` com `tipo: 2`, LC 116 e alíquota ISS).
3. Crie o documento `tipo: 200` com `clienteId`/`produtoId` nos snapshots (ou
   campos inline) e `Idempotency-Key`. A DPS é numerada por
   (`emitente`, 200, série, ambiente).
4. Mesmo fluxo acima: consulta por **chave**, cancelamento por **evento**
   (motivo conforme o manual), substituição **bloqueada** até confirmação.
5. DANFSe: `GET /api/fiscal/documentos/{id}/danfe` (simplificado local).

## Convenção de snapshots para NFS-e

- `SnapshotDestinatario`: `{"clienteId"?,"nome","documento","tipoPessoa"?,"ibge"?,"cep"?}`.
- `SnapshotItens`: **exatamente 1** item `{"produtoId"?,"descricao","itemLc116"?,"codTrib"?,"aliqIss"?,"vu"?,"qtd"?}`.
- Com IDs, os perfis fiscais são exigidos (422 lista pendências); sem IDs,
  valem os campos inline.

## Evidências (preencher manualmente)

| Data | UF/Município | Ambiente | Operações | Resultado | Pendências |
| --- | --- | --- | --- | --- | --- |
| — | — | homologação | — | não executado pelo agente | aguardando operador |
