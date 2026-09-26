# Etapa 38 — Fiscal-3: Municípios e situação da NFS-e

> Mapeamento do roteiro: Fiscal-3 = Etapa 38. Data-base 19/09/2026.

## Objetivo

Saber, por município (IBGE), se a NFS-e pode sair pelo Padrão Nacional e em quais ambientes — sem afirmar cobertura não implementada (R3).

## Entregas

- `Municipio` (IBGE 7 como identidade, `UX` parcial) + `NfseMunicipioConfig` (modo/fonte/atualizadoEm/override manual com motivo, `UX` IBGE) + `NfseAmbiente` (IBGE ou `"*"` padrão do modo × ambiente, URLs https, `verificado`, `UX` tripla) + `NfseImportRun` (histórico) + migration `AddNfseMunicipios`.
- Importador SuperAdmin `POST /api/fiscal/nfse/municipios/importar` (CSV ≤5 MB; cabeçalho opcional; rejeita vazio e lote <5 linhas salvo `ignorarMinimo`; nunca sobrescreve override manual; relatório com erro por linha + hash SHA-256).
- `GET /{ibge}/situacao` (tenant: modo + ambientes do município/padrão + mensagem pt-BR), `PUT /{ibge}` override com motivo, `PUT /{ibge}/ambientes` upsert, `GET` paginado com filtro uf/modo.
- `INfseParametrizacaoClient` (HTTP + cache 5 min; falha => Desconhecido; testes com handler falso; chamada real só manual).
- Seeds: `municipios-ibge.json` embarcado (São Paulo 3550308 + Jaú 3525300, ambos verificados no ibge.gov.br) + configs `Desconhecido` + ambientes `"*"` do modo nacional (hosts do roteiro §3, `verificado=false` — F0-15) + `GERAR-MUNICIPIOS.md` (carga total via API IBGE fora do runtime).

## Decisões

- IBGE como identidade; nome da cidade nunca é chave (R3/AUDITORIA).
- Adesão ≠ emissão: `SomenteAdn`/`ProvedorMunicipal` recebem mensagem orientativa, sem emissor.
- Sem XLSX nativo (CSV documentado; planilha oficial convertida pelo operador) — sem nova dependência nesta etapa.

## Validação

- Build 0 erros; **18/18 testes** (5 NFS-e novos: seed, import+override, vazio 400, TenantAdmin 403, mensagem); container com 2 municípios + 2 ambientes; memória + `PENDENCIAS.md` F0-15–F0-17.
