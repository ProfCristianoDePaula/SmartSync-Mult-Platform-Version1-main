# Etapa 37 — Fiscal-2: Cadastro prévio por estado (UF / SEFAZ)

> Mapeamento do roteiro: Fiscal-2 = Etapa 37. Data-base 19/09/2026.

## Objetivo

Catálogo global (SuperAdmin) de UFs, autorizadores e endpoints SEFAZ por ambiente — fonte que o admin do tenant/unidade usa para escolher UF e ambientes. Nada inventado (R3).

## Fontes oficiais consultadas (19/09/2026)

- Relação de Serviços Web do Portal Nacional: `https://www.nfe.fazenda.gov.br/portal/webServices.aspx` (produção) e `http://hom.nfe.fazenda.gov.br/portal/WebServices.aspx` (homologação) — via conteúdo indexado (fetch direto com erro de transporte; ver `PENDENCIAS.md` F0-13 para o que não foi confirmado).
- SEFAZ-SP: `https://portal.fazenda.sp.gov.br/servicos/nfe/Paginas/URL-WEBSERVICES.aspx/1000` (homologação SP + AN).
- IBGE: `ibge.gov.br` (SP=35 direto) + tabela citada da fonte IBGE para as demais cUFs (F0-14).
- Resultado: SVAN=MA; SVRS=AC,AL,AP,CE,DF,ES,PA,PB,PI,RJ,RN,RO,RR,SC,SE,TO; próprios=AM,BA,GO,MG,MS,MT,PE,PR,RS,SP.

## Entregas

- `UfFiscal` (`sigla/cUF/nome/autorizadores NFe-NFCe`, soft delete, `UX_sigla/cUF` parciais) + `SefazEndpoint` (`autorizador/modelo 55|65/serviço/ambiente/versão/url/vigência/fonte/verificado`, `UX` natural parcial) + enums + `ValueConverters` + migration `20260919164424_AddCatalogoUfSefaz`.
- Repositórios + `IUfFiscalService`/`ISefazEndpointService` (FluentValidation explícita + `BusinessRuleViolation` p/ duplicidade e host) + `SefazCatalogOptions` (`Fiscal:Sefaz:AllowedHostSuffixes`, default `.gov.br` — R8) + importação idempotente com relatório (criados/atualizados/inalterados/rejeitados + erro por linha).
- Controllers: `UfsFiscaisController` (escrita SuperAdmin; leitura `tenant`) + `SefazEndpointsController` (tudo SuperAdmin, incl. `POST /importar`); `GET /ufs/{uf}/ambientes` resolve endpoints pelos autorizadores da UF.
- Seed idempotente: **27 UFs** + **22 endpoints verificados** (AM homolog/produção 6+6, SP homologação 7, AN homologação 2 + produção 1), cada um com `fonteUrl + verificadoEm 19/09/2026`. SP-produção, SVRS/SVAN, NFC-e 65 e demais próprios **não** semeados (F0-13).
- Testes Testcontainers (7 novos, 13/13 no total): seed 27/SP-35, duplicidade UF/endpoint 400, host não-gov 400, TenantAdmin escreve 403, importação idempotente + linha rejeitada, ambientes SP (homologação cheia, produção vazia honesta).

## Decisões

- Endpoints pertencem ao **autorizador** (compartilhado), UFs referenciam autorizadores — por isso a chave única é (autorizador,modelo,serviço,ambiente,versão) entre ativos, como o roteiro pede.
- NFC-e herda o agrupamento da NF-e (hipótese documentada, F0-12); sem endpoints 65 semeados até a Fiscal-9.
- Rotas admin sem policy `tenant` (SuperAdmin sem claim precisa escrever — R5 só barra rotas operacionais).
- Auditoria em create/update/delete/import dos dois agregados (só metadados — R4).

## Validação

- `dotnet build Fiscal.slnx` — 0 erros (só NU1903 transitivos).
- `run-tests-in-docker.fiscal.ps1` — **13/13 Passed**.
- `docker compose up -d --build fiscal-api` — healthy; `fiscal_postgres`: `ufs_fiscais=27`, endpoints AM 12 / SP 7 / AN 3.

## Pendências

- F0-12–F0-14 novas; F0-01–F0-11 mantidas. Próxima: Fiscal-3 (Etapa 38) — municípios/IBGE + NFS-e.
