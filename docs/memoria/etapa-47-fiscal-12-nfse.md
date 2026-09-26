# Etapa 47 — Fiscal-12: NFS-e Padrão Nacional

> Mapeamento do roteiro: Fiscal-12 = Etapa 47.

## Objetivo

Emitir NFS-e no Padrão Nacional para municípios `NacionalEmissorPublico`, em homologação e produção.

## Entregas

- `INfseProvider` + `NfseNacionalProvider` (DPS + GZip + Base64 via mTLS ao Sefin do ambiente; consulta por chave; cancelamento por evento; substituição **bloqueada**; timeout propaga p/ conciliação) + `INfseHttpTransport` (seam testável).
- `NfseNacionalContrato` versionado (paths/nós + F0-28) + `DpsBuilder` (identificação/prestador/tomador/serviço/valores + IBSCBS; tamanhos do manual).
- `NfsePipelineAdapter` (IAutorizadorFiscal): DPS numerada via `SerieNumeracao` (modelo 200), perfis por `clienteId`/`produtoId` do snapshot ou campos inline, NFS-e multi-serviço recusada; fora do nacional → 422 `MunicipioSemEmissaoNacional`.
- Seletor: NFSe + homolog/produção → adapter nacional; parametrização conferida (divergência só avisa, nunca calcula).
- DANFSe local simplificado (decisão: serviço oficial como evolução) + `HOMOLOGACAO.md` (convenção de snapshots + MEI).

## Decisões (delegadas)

- Contratos DPS/HTTP a partir do padrão documentado, tudo com F0-28; sem centenas de webservices municipais.

## Validação

- Build 0 erros; **69/69 testes** (DPS, 422, pipeline simulador + DANFSe, fixtures emitir/consultar/cancelar); container healthy.
