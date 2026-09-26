# Etapa 46 — Fiscal-11: Armazenamento e DANFE/DANFCE

> Mapeamento do roteiro: Fiscal-11 = Etapa 46.

## Objetivo

Guardar XML/eventos/respostas com integridade e acesso isolado; DANFE/DANFCE baixáveis com marcação de homologação.

## Entregas

- `IArmazenamentoFiscal` (disco/volume `{raiz}/{tenant}/{ano}/{mes}/{chave}/`, SHA-256 lado a lado, sanitização anti-travessia, retenção configurável default nunca-apagar — R9) + `ArquivadorDocumentos` (best-effort no worker/conciliação: `enviado.xml` + `resposta.json` + `evento.json`).
- `PdfSimples` (PDF 1.4 sem deps) + `DanfeGerador` (DANFE/DANFCE simplificado com chave/protocolo/ambiente + "SEM VALOR FISCAL" em homologação; DANFSe na Fiscal-12).
- Endpoints: `GET /arquivos`, `/arquivos/{nome}`, `/danfe` (todos com posse do tenant; travessia 400/404).

## Decisões (delegadas)

- PDF simplificado (layout oficial completo = F0-27); dados sempre do XML/snapshot oficial; PDF nunca substitui o XML.

## Validação

- Build 0 erros; **65/65 testes** (XML idêntico via hash, isolamento + travessia, PDF válido com marcação); container healthy.
