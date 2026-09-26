# FONTES — Módulo Fiscal

> Registrar fonte, versão, data de consulta e vigência. Documentação ≠ endpoint de emissão. Falta de acesso é pendência, não integração.

## Oficiais (do briefing, ainda por inspecionar na implementação)

- Portal Nacional da NF-e — https://www.nfe.fazenda.gov.br/ — documentação/serviços oficiais. Consulta pendente na Etapa 7 (manual, NTs, schemas, relação de serviços por UF/ambiente/versão).
- Serviços oficiais de NF-e — https://www.nfe.fazenda.gov.br/portal/webservices.aspx — idem acima.
- SEFAZ-SP — https://portal.fazenda.sp.gov.br/ — verificar autorizador próprio/virtual, contingência e homologação (Etapa 7, SP primeiro se aplicável).
- Monitoramento das Adesões à NFS-e — https://www.gov.br/nfse/pt-br/municipios/monitoramento-adesoes — aponta para painel/tabela de conveniados; formato atual e API pública anônima **não confirmados** (verificação na Etapa 3; sem `GET /municipios-conveniados` inventado).
- APIs de produção restrita e produção (NFS-e) — https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/apis-prod-restrita-e-producao — página de documentação (SEFIN Nacional, parâmetros municipais, ADN, CNC, DANFSE); inspecionar contrato/autorização de cada API na Etapa 8, sem gravar como endpoint de emissão.
- Documentação atual (NFS-e) — https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/documentacao-atual — manuais de contribuintes vs municípios, leiautes, schemas, tabelas de domínio; não usar APIs administrativas de prefeituras como API de lojista (Etapa 8).

## Código-fonte (verificadas em 2026-09-19, HEAD `3c9a52d`)

- `Identity.Infrastructure.csproj:12-21`, `Estoque.Infrastructure.csproj:12-16` — net10.0, EF Core 10.0.10, Npgsql 10.0.3.
- `IdentityDbContext.cs:8-24`, `EstoqueDbContext.cs:13-33` — fronteiras de banco.
- `Tenant.cs`, `Branch.cs`, `Address.cs` (ambos os domínios), `Documento.cs`, `TenantConfiguration.cs:35-59`, `BranchConfiguration.cs:44-75`.
- `ApplicationUser.cs:11-28`, `Roles.cs:8-13`, `JwtClaims.cs`, `TokenService.cs:49-62`, `ClaimsPrincipalExtensions.cs:15-29`.
- `Product.cs:9-29`, `Pedido.cs:16-23`, `Venda.cs:14-27`, `FormaPagto.cs`, `VendaService.cs:51-132`, `CalculoFreteService.cs:11-33`.
- `NfeXmlParser.cs:12-59`, `XmlImportWorker.cs`, `XmlImport.cs`, `OutboxMessage.cs`, `OutboxDispatcherWorker.cs:37-58`, `IdentityApiClient.cs:17-76`.
- `Module.cs`, `TenantModule.cs`, `TenantModuleService.cs`, `ModuleActiveRequirement.cs`, `ModuleAccessChecker.cs`.
- `DbSeeder.cs:119-127` (Jaú/SP), `BranchesTests.cs:37-84` (São Paulo/SP).
- Builds: `dotnet build Identity.slnx / Estoque.slnx` (2026-09-19); `docker compose ps` + `/api/health`.

## Material didático

- `docs/fiscal/material-base.md` — **recebido em 19/09/2026** (conteúdo colado pelo usuário; conversa original 19/09/2026; fundamentos fiscais + roteiro backend com NF-e 55, CT-e 57/67, NFC-e 65, NFS-e, certificado A1/A3/nuvem, XML/XSD/SOAP, simulador → homologação → produção). Didático, exemplos não compilados como solução completa. Confronto obrigatório na Etapa 2+ sem replicação literal. Divergências já mapeadas: `SqlServer` (§9.2) vs PostgreSQL/Npgsql do repo; `UrlAutorizacao` configurável vs catálogo confiável da Etapa 3; `NFe` gigante vs contratos especializados por documento; `SELECT+INSERT` vs restrição única + reserva atômica.
