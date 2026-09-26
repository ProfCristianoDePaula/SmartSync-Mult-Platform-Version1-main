# Etapa 39 — Fiscal-4: Perfil fiscal do emitente e ambientes

> Mapeamento do roteiro: Fiscal-4 = Etapa 39.

## Objetivo

Admin do tenant/unidade configura preferências fiscais por filial, com homologação e produção separadas e produção travada por gate.

## Entregas

- `EmitenteFiscal` (tenant+filial únicos; `CnpjFiscal` com DV clássico, alfanumérico sinalizado, **CPF → 422** P3; IE/IM/CNAE/CRT; `FiscalAddress` com IBGE; fone/e-mail) + `ConfiguracaoDocumento` (emitente×tipo×ambiente, série, `ModoIntegracao` padrão Simulador, `ReferenciaCertificado?`, `CscId?`; produção só via promoção) + `ConcessaoUnidade` (tenant+filial+user+papel Configurar|Emitir) + migration `AddEmitenteFiscal`.
- Filial validada no Identity via HTTP + cache, **fail-closed na escrita** (diferença consciente do Estoque v1).
- `EmitenteFiscalService`: CRUD com escopo (TenantAdmin total; Manager/Seller só com concessão Configurar, senão 403), UF/município contra catálogo, configs padrão 3 tipos × 2 ambientes no create, produção bloqueada no upsert direto.
- `GetProntidaoAsync`: checklist por tipo/ambiente (cadastro, UF, município, série, CSC p/ NFC-e, certificado via `ICertificadoReadModel`, conexão homologação pendente até Fiscal-10).
- `PromoverProducaoAsync`: texto exato `PROMOVER PARA PRODUCAO` + papel TenantAdmin + flag `Fiscal:ProducaoHabilitada` (409 se false) + prontidão completa + auditoria.
- `ConcessaoService` + controller (TenantAdmin concede/revoga/lista).
- Controllers + `FiscalOptions` (`appsettings` + compose `false`).
- `ICertificadoReadModel` + `SemCertificados` (placeholder documentado, trocado na Fiscal-5 sem mudar interface).

## Decisões (delegadas)

- P3 sem consulta ao Identity: CNPJ de 11 dígitos é recusado no próprio VO (422 `fiscal.emitente.cpf-bloqueado`).
- Sem `UnitAdmin`: concessão por Branch; Client nunca recebe.
- Token do CSC fica para o cofre (Fiscal-5); só `CscId` nesta etapa.
- Manager/Seller **configuram** com concessão Configurar; concessão Emitir passa a valer na Fiscal-8.

## Validação

- Build 0 erros (só NU1903); **26/26 testes** (8 novos: isolamento, CPF 422, CNPJ 400, Manager 403→201 com concessão, prontidão pendente, promoção 409/400); container healthy, tabelas criadas.
