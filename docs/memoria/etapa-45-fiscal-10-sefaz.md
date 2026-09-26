# Etapa 45 — Fiscal-10: Transmissão em homologação, eventos, inutilização

> Mapeamento do roteiro: Fiscal-10 = Etapa 45. Nenhuma chamada real pelo agente.

## Objetivo

Cliente SEFAZ real restrito à homologação, usando o catálogo da Fiscal-2.

## Entregas

- `SefazSoapClient` (SOAP 1.2, mTLS por certificado, Fault/timeout/limite 10 MB, `Interpretar` com cStat/motivo/protocolo/recibo) + `ISefazConnectionFactory` (handler por thumbprint+validade, descarte por cache, sem bypass TLS).
- `SoapEnvelopes` por serviço 4.00 (Status/Autorização-lote síncrono/RetAutorizacao/Consulta/Inutilizacao/Evento) — confrontar WSDL antes de homologar (F0-24).
- `CStatTabela` versionada (100/101/102/135/107 autorizam; 103-105 aguardam; resto rejeita; denegados preservados como rejeição local) + `SefazAutorizador` (monta via assembler → XSD oficial → assina → transmite pelo catálogo; recibo → polling; produção só com flag).
- `INFeAssembler` (snapshots JSON → `NFeBuildInput`; campo ausente = erro acionável) + `IAutorizadorSelector` (modo da config; worker e conciliação usam o destino fixado).
- `POST /emitentes/{id}/testar-conexao` (StatusServico homologação; online marca `HomologacaoValidadaEm`, alimenta a prontidão) + migration `AddHomologacaoValidada`.
- `docs/fiscal/HOMOLOGACAO.md` (roteiro manual NF-e + NFS-e + tabela de evidências).
- Contingência: sem modo offline (F0-25); indisponibilidade → Desconhecido/ErroTecnico.

## Decisões (delegadas)

- Envelopes a partir do padrão dos WSDLs (F0-24); `tPag` real na Fiscal-13 (aqui "99"); recibo preservado no protocolo até autorizar.

## Validação

- Build 0 erros; **62/62 testes** (contratos SOAP com fixtures: autorizado/rejeitado/fault/timeout; transmissão homolog e2e com fakes; testar-conexão marca prontidão; envelopes + cStat); container healthy.
