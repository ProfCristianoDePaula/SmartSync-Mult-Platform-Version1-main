# Etapa 42 — Fiscal-7: Núcleo do documento fiscal

> Mapeamento do roteiro: Fiscal-7 = Etapa 42.

## Objetivo

Domínio do documento, numeração concorrente segura e chave de acesso — sem transmissão (Fiscal-10).

## Entregas

- `DocumentoFiscal` (tipo/ambiente/status/série/número/chave/protocolo/cStat/origem venda-pedido/idempotência+hash/snapshots JSON imutáveis; `UX (tenant,idempotency)` absoluta + `UX` chave; **sem delete** — R9) com máquina de estados testada (10 estados; sem Autorizado→* exceto Cancelado; sem retorno) + `RegistrarRetorno` + `Renumerar` (só de Rejeitado/ErroTecnico, sempre p/ frente — número rejeitado nunca reutilizado).
- `EventoFiscal` (append-only) + `SerieNumeracao` (emitente/modelo/série/ambiente, `UX` absoluta) + migration `AddDocumentoFiscal`.
- `ISerieNumeracaoService.ReservarAsync`: `INSERT...ON CONFLICT DO UPDATE...RETURNING` em 1 instrução via ADO.NET na transação ambiente (R7; correção: `SqlQueryRaw` rejeita UPDATE não-composto).
- `ChaveAcesso` (44 posições, DV módulo 11, parse de campos; CNPJ alfa recusado F0-01) + `IDocumentoFiscalRepository` (por id + idempotência).

## Decisões (delegadas)

- Snapshots como JSON text (imutáveis por construção: sem setters); NFS-e usa modelo 200 na sequência (DPS na Fiscal-12).
- Vetores oficiais de chave ainda pendentes (F0-21); testes com autoconistência + tamper.

## Validação

- Build 0 erros; **44/44 testes** (50 reservas concorrentes únicas 1–50; transições; DV + adulteração + alfa; snapshot + renumeração; idempotência no banco); container healthy.
