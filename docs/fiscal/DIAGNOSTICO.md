# Diagnóstico — Módulo Fiscal (Etapa 1)

> Somente diagnóstico/documentação. Nenhum comportamento de negócio foi alterado nesta etapa.
> HEAD local verificado: `3c9a52dfaf2463f51b3e8f7774654725ae5f952e` (`feat: Implement new services for order processing and coupon management`) — idêntico ao commit de referência do briefing. Branch: `main`.
> Material didático `docs/fiscal/material-base.md` **não localizado** no repo (ver §10). Diagnóstico feito sobre o código real.

Data: 2026-09-19. Stack real confirmada antes de qualquer proposta.

## 1. Base verificada

| Área | Evidência (caminho:linha) | Situação |
| --- | --- | --- |
| Soluções | `Identity.slnx:3-6`, `Estoque.slnx:3-6` | Duas soluções, camadas `Api/Application/Domain/Infrastructure` + `tests/` em cada. Sem solução Fiscal. Sem solução paralela a criar |
| Runtime | Todos os 10 `.csproj` com `<TargetFramework>net10.0</TargetFramework>` (`src/Identity.Api/Identity.Api.csproj:4`, `src/Estoque.Api/Estoque.Api.csproj:4`, demais Domain/Application/Infrastructure/Tests) | C# / ASP.NET Core 10 confirmado. Não trocar |
| Banco/provider | `src/Identity.Infrastructure/Identity.Infrastructure.csproj:13-14,21`, `src/Estoque.Infrastructure/Estoque.Infrastructure.csproj:12-14`: `Microsoft.EntityFrameworkCore 10.0.10`, `Relational 10.0.10`, `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`; `UseNpgsql` em `src/Identity.Infrastructure/DependencyInjection.cs:40-41`, `src/Estoque.Infrastructure/DependencyInjection.cs:36-37` | PostgreSQL + EF Core Code First. Preservar; nada de SQL Server/MariaDB |
| Migrações | `src/Identity.Infrastructure/Persistence/Migrations/` (ex.: `20260808151153_InitialTenantAndBranches.cs`, `20260808153512_AddIdentity.cs`, … `MakePlanLimitsNullable`), `src/Estoque.Infrastructure/Migrations/` (`20260822190930_InitialCreate.cs`, `20260912140738_AddPedidosVendas.cs`); factories `src/Identity.Infrastructure/Persistence/IdentityDesignTimeDbContextFactory.cs:13-15`, `src/Estoque.Infrastructure/Persistence/EstoqueDesignTimeDbContextFactory.cs:11-17` | Code First com snapshot. Novas tabelas fiscais devem seguir migrações aditivas + backfill, só em banco local descartável |
| Fronteiras de dados | `src/Identity.Infrastructure/Persistence/IdentityDbContext.cs:8-24` (Tenants, Branches, Plans, Modules, TenantModules + Identity); `src/Estoque.Infrastructure/Persistence/EstoqueDbContext.cs:13-33` (Products…Alerts + Cupons/Pedidos/Vendas + `OutboxMessages`) | Dois bancos/serviços. Não acoplar DbContexts nem impor FK entre bancos |
| Auth | ASP.NET Identity no Identity (`src/Identity.Infrastructure/Persistence/IdentityDbContext.cs:8-9` : `IdentityDbContext<ApplicationUser,ApplicationRole,Guid>`; `src/Identity.Infrastructure/DependencyInjection.cs:43-62` com `RequireConfirmedEmail=true`); JWT RS256 próprio + JWKS (`src/Identity.Infrastructure/Security/SigningKeyProvider.cs:10,27-28`, `src/Identity.Infrastructure/Security/TokenService.cs:54-62`, `src/Identity.Api/Endpoints/JwksController.cs:30-39`, `src/Identity.Api/Program.cs:172-179`); Estoque valida via JWKS remoto (`src/Estoque.Api/Program.cs:111-141`, `src/Estoque.Infrastructure/Identity/JwksKeyStore.cs`, `AddHostedService<JwksRefreshService>` em `src/Estoque.Infrastructure/DependencyInjection.cs:50-51`) | Preservar Identity + JWT. Não criar autenticação paralela |
| Claims/contexto | `src/Identity.Domain/Common/JwtClaims.cs:9-22` (`user_id,tenant_id,role,full_name,email,email_confirmed,phone_confirmed,profile_complete`); cópia de contrato em `src/Estoque.Domain/Common/JwtClaims.cs:11-32`; emissão em `src/Identity.Infrastructure/Security/TokenService.cs:49-52`; leitura em `src/Estoque.Api/Extensions/ClaimsPrincipalExtensions.cs:15-29` (`GetUserId/GetTenantIdOrNull/GetRequiredTenantId`) | TenantId e user_id vêm do token. Regra permanente 1 já atendida no padrão atual |
| Roles | `src/Identity.Domain/Common/Roles.cs:8-13`: `SuperAdmin,TenantAdmin,Manager,Seller,Delivery,Client` | Não existe `UnitAdmin`. `Manager` não é admin de todas as unidades por padrão |
| Policies | Identity `src/Identity.Api/Program.cs:52-57` (`email-confirmed`); Estoque `src/Estoque.Api/Program.cs:60-73` (`tenant` exige `tenant_id`; `module-estoque` via `ModuleActiveRequirement`); usos ex.: `src/Identity.Api/Endpoints/TenantsController.cs:16` (SuperAdmin), `src/Identity.Api/Endpoints/BranchesController.cs:22` (SuperAdmin,TenantAdmin), `src/Estoque.Api/Endpoints/CarrinhoController.cs:12`, `PedidosController.cs:12` (`Policy="tenant"`) | Reaproveitar; criar permissões fiscais novas com escopo por recurso (Etapa 2) |
| Módulos/licença | `src/Identity.Domain/Entities/Module.cs:20-22,30-38` (slug estável/imutável), `src/Identity.Domain/Entities/TenantModule.cs:42-60` (máx. 1 ativo por tenant+module, troca = inativa + cria nova), `src/Identity.Infrastructure/TenantModules/TenantModuleService.cs:55-193`; gate `src/Estoque.Api/Auth/ModuleActiveRequirement.cs:21-31` + `src/Estoque.Infrastructure/Identity/ModuleAccessChecker.cs:20-41` (cache 5 min, fail-closed) | Habilitação fiscal via `Module/TenantModule` (ex.: slug `fiscal`). Licença ≠ autorização sobre documentos |
| Background | `src/Estoque.Infrastructure/DependencyInjection.cs:107-110`: `ExpiryScanWorker,ReplenishmentScanWorker,XmlImportWorker,OutboxDispatcherWorker`; Identity sem workers próprios listados | Reutilizar padrão `BackgroundService + IServiceScopeFactory` |
| Outbox atual | `src/Estoque.Infrastructure/Persistence/Outbox/OutboxMessage.cs:9-16` (`Type,PayloadJson,PublishedAtUtc`); `src/Estoque.Infrastructure/Persistence/Outbox/OutboxService.cs:20-68`; `src/Estoque.Infrastructure/Jobs/OutboxDispatcherWorker.cs:37-58` (polling 30 s, batch 100, só marca `PublishedAtUtc`) | Não é prova de entrega/consumo fiscal. Etapa 6 exigirá lease/ack/retry/conciliação |
| Identity→Estoque HTTP | `src/Estoque.Infrastructure/Identity/IdentityApiClient.cs:17-37,40-76` encaminha `Authorization` do `IHttpContextAccessor` (`GET api/tenants/me/modules`, `GET api/tenants/{t}/branches/{b}` com `Allowed/Denied/Unknown`) | Worker sem HttpContext não pode reutilizar JWT de usuário. Exigirá identidade de serviço + contexto persistido |
| Storage | Postgres por serviço + Valkey (`docker-compose.yml`: `postgres,estoque-postgres,valkey`); sem blob storage dedicado no repo | XML/PDF fiscais exigirão armazenamento com controle de acesso (decisão Etapa 2/6, sem PFX em wwwroot) |
| Testes | `tests/Identity.Tests/`, `tests/Estoque.Tests/` (xUnit + Testcontainers Postgres, ex.: `BranchesTests.cs`, `StockFlowTests`); scripts `scripts/run-tests-in-docker.ps1`, `run-tests-in-docker.estoque.ps1` | Baseline abaixo. Novos testes fiscais seguem este padrão |
| Frontend | Nenhum projeto web/mobile no repo (só 8 pastas em `src/`; Dockerfiles só empacotam `Identity.Api.dll`/`Estoque.Api.dll`; `docker-compose.yml` sem serviço UI; única UI é Scalar/OpenAPI dev em `src/Identity.Api/Program.cs:228-241`, `src/Estoque.Api/Program.cs:154-163`) | Confirmar se UI vive em outro repositório antes de criar telas (Etapa 4/10). Nesta etapa, nenhuma UI criada |

Baseline build/testes (2026-09-19, sem alterar código):
- `dotnet build Identity.slnx`: êxito, 0 erros, 2 avisos (NU1903 `SSH.NET 2025.1.0` vulnerabilidade conhecida — preexistente, fora do escopo fiscal).
- `dotnet build Estoque.slnx`: êxito, 0 erros, 3 avisos (NU1903 idem + CS8620 `ValueConverter<Barcode,string>` vs `Barcode?` em `ProductConfiguration.cs:40` — preexistentes).
- Containers em execução: `identity_api, estoque_api, identity_postgres, estoque_postgres, identity_valkey` todos `healthy`; `GET /api/health` 200 em `:8080` e `:18081`.
- Suítes completas não executadas nesta etapa (diagnóstico sem mudança não exige re-execução; último estado registrado: Identity/Estoque com suítes Testcontainers passando antes desta etapa).

## 2. Mapa fiscal do domínio existente

| Requisito fiscal | Componente existente (caminho:linha) | Lacuna | Alteração proposta (Etapa 2+) | Risco se ignorado |
| --- | --- | --- | --- | --- |
| Estabelecimento emitente (CNPJ/IE/IM por local de emissão) | `Branch` = `TenantId+Name+Address+Contact` (`src/Identity.Domain/Entities/Branch.cs:11-19`), sem documento próprio; `Tenant.Documento` CPF/CNPJ único global (`src/Identity.Domain/Entities/Tenant.cs:15-19`, owned `tipo_pessoa/documento` em `TenantConfiguration.cs:35-59`, imutável) | Todas as unidades compartilham o CNPJ do tenant por construção. Sem IE/IM, sem CNPJ por filial, sem flag matriz/filial, sem vínculo unidade↔CNPJ | Modelar `EstabelecimentoEmitente` referenciando `Branch` por ID (sem FK cross-DB se Fiscal ficar separado; snapshot de IDs), com CNPJ/IE/IM próprios, vigência e unicidade por CNPJ+tenant; bloquear emissão sem estabelecimento determinado (Pedido.IdUnidade nulo ou sem emitente) | Emitir com CNPJ errado; sequências/certificados trocados entre filiais; autuação |
| Unidade operacional vs estabelecimento | `Pedido.IdUnidade` (`BranchId?`) em `src/Estoque.Domain/Entities/Pedido.cs:20`; `Venda.TenantId + IdPedido` (`src/Estoque.Domain/Entities/Venda.cs:14-17`) — unidade e cliente alcançados via `Pedido` | `Venda` não carrega `BranchId` direto; `Pedido.IdUnidade` é nullable; sem distinção loja vs estabelecimento | Resolver emitente no rascunho e fixar snapshot (`tenant,branch,emitente,CNPJ,ambiente,sequência`) no documento/tentativa; recusar configuração ambígua entre unidades do mesmo estabelecimento | Redirecionamento de documento pendente após mudança de config; numeração duplicada |
| Cliente fiscal | `ApplicationUser`: `TenantId?,FullName,Document?,ProfileComplete` (`src/Identity.Infrastructure/Persistence/Identity/ApplicationUser.cs:11-28`); `Pedido.IdCliente: Guid?` = `user_id` do JWT (`src/Estoque.Api/Endpoints/CarrinhoController.cs:27-92` via `WithContext(GetRequiredTenantId(),GetUserId())`); sem FK entre bancos | `Document` é string livre, sem tipo/IE/endereço fiscal/município IBGE; sem endereço do cliente; `ProfileComplete` ≠ perfil fiscal completo | Complementar por perfil fiscal vinculado ao usuário (mecanismo de perfil/contrato, sem duplicar login), com pessoa, identificação, endereço + IBGE, indicador IE; snapshot imutável no documento | Rejeição SEFAZ/prefeitura por destinatário incompleto; LGPD se duplicar dados sem necessidade |
| Endereço/UF/município | `Address` (Identity `src/Identity.Domain/ValueObjects/Address.cs:8-44`, Estoque `src/Estoque.Domain/ValueObjects/Address.cs:8-45`): `Street,Number,Complement?,District,City (texto),State (UF 2 letras),PostalCode (8 dígitos)`; colunas `address_*` (`BranchConfiguration.cs:44-55`, `SupplierConfiguration.cs:67-69`) | Sem código IBGE em nenhum tipo/tabela; `City` é texto livre (ex.: seed `Jaú/SP` em `DbSeeder.cs:119-127`; testes `São Paulo/SP`) — nome de cidade não serve como chave de roteamento | Adicionar identificação municipal (IBGE) com migração + backfill a partir de `City/UF/CEP` com revisão humana; nunca usar nome da cidade como chave de provedor/autorizador | Roteamento NFS-e para provedor errado; Gemeinde sem adesão tratada como coberta |
| Produtos (NF-e) | `Product`: `TenantId+Sku+Name+Barcode?+Brand/Model/Category?+UoM` (`src/Estoque.Domain/Entities/Product.cs:18-29`), jamais duplicado por filial (`:9-12`); `Supplier`: CPF/CNPJ por tenant | Sem NCM/CEST/origem/unidades comercial-tributável/conversão/GTIN, sem perfil fiscal | Perfil fiscal de item por emitente/operação (NCM/CEST etc. como texto com zeros preservados), fora do `Product` canônico; snapshot no documento | Classificação tributária inventada; rejeição de schema; alíquota presumida |
| Serviços (NFS-e) | Nenhum catálogo de serviços localizado (só `Product` de mercadoria) | Sem código nacional/municipal, discriminação, retenções, ISS | Catálogo de serviços por tenant + parâmetros por operação; códigos como texto | ISS no município errado; retenção indevida |
| Operação/tributos | `Pedido` (carrinho→fechado), `ProdutosPedido` (`IdPedido+IdProduto+Quantidade`), `Venda` (`IdPedido+FormaPagto+Bruto/Desconto/Líquido/Frete/Final+IsPago+NrPedido+Parcelas`), `FormaPagto` global seedada | Sem CFOP/CST/CSOSN, sem NCM por item, sem natureza de operação, regime, finalidade, origem/destino, sem campos da reforma; `Venda.IsPago=false` inicial, frete mock (`CalculoFreteService.cs:11-23`) | Perfis de operação por emitente/regime/data/finalidade/origem-destino/tipo destinatário + validação/prévia com decimais e totais; matriz de operações suportadas vs pendentes | Tributo calculado por regra inventada; devolução/ST/importação anunciadas sem implementação |
| Pagamentos vs fiscal | `FormaPagto` (Pix/Transferência/Depósito/Débito=1, Crédito=12 via `EstoqueDbInitializer.cs:26-35`); `Venda.QuantidadeParcelar` validada | Pagamento é cadastral; sem gateway/efetivação real | Não relançar financeiro na reemissão; eventos idempotentes; preservar responsabilidade dos módulos existentes | Duplo lançamento financeiro/estoque em retry |
| Estoque vs emissão | `StockMovement` append-only + `StockBalance` por (tenant,produto,filial) via `MovementApplier`; `VendaService.FinalizarCompraAsync` **não baixa estoque** (comentário explícito) | Acoplamento indevido se fiscal baixar estoque de novo | Fiscal não movimenta estoque; consome `Venda` como fato; idempotência entre emissão repetida e estoque/financeiro | Baixa duplicada em retry de emissão |
| Importação XML existente | `NfeXmlParser.cs:12-59` extrai só `det/prod/cProd/xProd/qCom/vUnCom/uCom/lote/validade`; `XmlImportWorker` cria produtos ausentes + entradas | É importador de entrada, não emissor/validador/assinador; não lê `emit/dest/ide/total/protNFe/chave` | Preservar importação; criar contratos de saída próprios (serialização/assinatura/mTLS/SOAP) em código novo | Transformar parser simplificado em gerador autorizado |
| Emissão atual | Grep fiscal (`IBGE|IE|IM|CNPJ|cStat|chNFe|protNFe|ICMS|CFOP|NCM|DANFE|SEFAZ|certificado|PFX|mTLS|SOAP|DPS|RPS|CT-e|MDF-e`): só hits de importação (`NfeXmlParser.cs:6`, `ImportsAndAlertsControllers.cs:13`, `XmlImportWorker.cs:14`, `XmlImport.cs:7`) + tokens JWT com "emitido" | Nenhuma emissão NF-e/NFS-e iniciada; nenhum certificado/endpoint fiscal | Tudo do §3 em diante é greenfield integrado, não continuação |
| Permissão por unidade | Autorização por claim `tenant_id` + roles globais; `BranchesController` SuperAdmin/TenantAdmin; sem associação usuário↔Branch | `Manager/Seller` sem escopo por filial; SuperAdmin sem `tenant_id` opera qualquer tenant | Associação/concessão por `Branch` (Etapa 2): visualizar/configurar/gerir credencial/emitir/cancelar/baixar XML/ativar produção/administrar catálogo; validar em leitura, escrita, download, job e consulta admin | Vazamento entre unidades/tenants; emissão arbitrária cross-tenant |
| Segredos | Nenhum campo de certificado/credencial fiscal; `DataProtection`/`jwtkeys` fora do banco; `.env` fora do git | Sem cofre; risco de PFX em wwwroot/logs se improvisado | Cofre ou cripto autenticada com chave fora do banco, metadados sem segredo, write-only, rotação/auditoria (Etapa 4) | Vazamento de chave privada; uso entre emitentes |
| Catálogo autorizadores | Nenhuma tabela de UF/autorizador/provedor/versão/ambiente; `Module` slug `core` (+ `estoque` em runtime) | Sem SEFAZ virtual/provedor municipal versionado | Catálogo global versionado (Etapa 3) com integração marcada disponível só quando verificada |
| Multi-CNPJ | `Branch` sem documento → 1 CNPJ por tenant hoje | Filiais com CNPJ próprio (varejo real) não representáveis | Emitente resolve N unidades → 1 CNPJ ou 1:1; sequência/certificado por emitente, não por loja |
| Frontend | Ausente neste repo (ver §1) | Telas admin/operacionais sem local | Confirmar repositório de UI; até lá, APIs + contratos completos, sem declarar telas prontas |

## 3. Arquitetura incremental proposta (sem recriar o sistema)

- **Módulo Fiscal como novo bounded context** com camadas equivalentes (`Fiscal.Domain/Application/Infrastructure/Api` ou pastas equivalentes dentro das fronteiras atuais, a decidir na Etapa 2 após confirmar convenções de `AGENTS.md` — ausente nesta etapa). **Não acoplar DbContexts** nem criar FK entre bancos Identity↔Estoque↔Fiscal.
- **Persistência:** tabelas fiscais no **mesmo Postgres do contexto que detém `Venda`** (hoje `estoque`) *ou* banco Fiscal próprio com replicação por eventos — decisão na Etapa 2 com prova de transação (rascunho→outbox na mesma transação do pedido exige mesmo banco; se separar, rascunho vira evento + worker). Em qualquer caso: referências a `Tenant/Branch/Product/Pedido/Venda/ApplicationUser` por **IDs + snapshots imutáveis**, nunca FK cross-DB; consistência por contratos/eventos versionados.
- **Identidade fiscal:** `EstabelecimentoEmitente` (por `Branch`, com CNPJ/IE/IM/vigência/regime/séries/ambiente por documento) + perfil fiscal de cliente (extensão de `ApplicationUser` sem duplicar login) + preferência tenant→override unidade com origem do valor efetivo visível. Certificado/credencial por **referência** (Etapa 4).
- **Catálogo global** (admin plataforma): UF→autorizador→serviço/operação→ambiente→versão/vigência + municípios IBGE + provedores NFS-e + capacidades; tenant/unidade apenas **seleciona** opções (Etapa 3).
- **Motor durável** (Etapa 6): rascunho→validação→fila (outbox transacional)→worker com lease, retry/backoff, conciliação por chave fiscal, idempotência `tenant/emitente/ambiente/operação/chave + hash payload`. `OutboxDispatcherWorker` atual não serve como entrega fiscal.
- **Integrações reais** (Etapas 7–9): adapters por autorizador/provedor com SDK mantida quando adequada; sem URLs inventadas; homologação separada de produção (desativada por padrão).
- **Operação** (Etapas 10–12): eventos (CC-e/inutilização só NF-e; substituição NFS-e só se provedor suportar), DANFE/DANFSE, listagem/timeline/downloads autorizados, homologação e liberação de produção por emitente/documento/ambiente.

## 4. Escopo inicial de operações fiscais (a confirmar na Etapa 5)

NF-e modelo 55 e NFS-e nacional primeiro; CT-e/NFC-e/MDF-e fora. Operações candidatas (a validar contra vendas reais): venda interna SP com ICMS próprio sem ST, prestação de serviço municipal com ISS próprio sem retenção. Devolução, importação/exportação, ST, retenções complexas e contingência entram como **pendentes** até implementação correspondente. Nenhuma operação é declarada suportada antes de perfil + adapter + homologação.

## 5. UFs/municípios encontrados (código real, sem IBGE)

- Seed: `Jaú/SP`, CEP `17206441` (`DbSeeder.cs:119-127`). Testes/docs: `São Paulo/SP`, CEP `01310100` (`BranchesTests.cs:37-84`, `CONTRATO-IDENTIDADE.md:428`, `FLUXO-DE-CADASTRO.md:238`).
- **IBGE:** zero ocorrências em `src/` (grep §1). UF aparece só como `State` 2 letras; município só como `City` texto.
- Cobertura inicial sugerida: **SP** (SEFAZ-SP virtual ou própria, a verificar na Etapa 3/7) + município da sede para NFS-e nacional; demais UFs/municípios entram no catálogo como **cadastrados, sem integração disponível** até verificação.

## 6. Plano das etapas seguintes (mantendo prompts)

1. **Etapa 2:** modelo fiscal + permissões por Branch + migrações aditivas locais + snapshots; sem tocar cadastro de produto/cliente canônico.
2. **Etapa 3:** catálogo UF/autorizador + municípios IBGE com importação administrativa validada (sem Power BI não documentado como dependência); sem afirmar cobertura.
3. **Etapa 4:** cofre/cripto + upload A1 + diagnósticos (sem emitir nota real).
4. **Etapa 5:** perfis cliente/produto/serviço + operação + prévia fiscal com matriz suportado/pendente.
5. **Etapa 6:** motor durável com lease/ack/idempotência/conciliação; sem rebaixar estoque/financeiro.
6. **Etapas 7–9:** adapters NF-e SP + NFS-e nacional + municipais prioritários conforme UFs/municípios reais das unidades.
7. **Etapas 10–12:** eventos/DANFE/listas, homologação com matriz honesta, liberação de produção por emitente.

## 7. Riscos e limites assumidos nesta etapa

- Material didático ausente (§10) — snippets não foram confrontados; Etapa 2 não deve replicar anexo literalmente.
- Preço fiscal: `Venda` usa `PrecoMock=100` (`VendaService.cs:90`) — prévia fiscal exigirá preço real por item antes de qualquer homologação.
- Frete mock, `ITenantParcelamentoProvider` nulo, `IsPago=false` inicial — fora do caminho fiscal, mas mostram que valores externos ainda são simulados.
- Rate limiting, cache Valkey e OTel existem; não substituem lease fiscal, idempotência externa nem conciliação.

## 8. Arquivos alterados nesta etapa

- Nenhum arquivo de código. Somente `docs/fiscal/` (Etapa 1): `DIAGNOSTICO.md` (este), `STATUS.md`, `FONTES.md`, `DECISOES.md`.

## 9. Testes executados

- `dotnet build Identity.slnx` — êxito, 0 erros (2 avisos NU1903 preexistentes).
- `dotnet build Estoque.slnx` — êxito, 0 erros (3 avisos NU1903/CS8620 preexistentes).
- `docker compose ps` — 5/5 `healthy`; `GET /api/health` 200 em `:8080` e `:18081`.
- Suítes xUnit não re-executadas (etapa sem código); estado anterior registrado nas memórias do repo.

## 10. Perguntas realmente bloqueantes

1. **Onde está `docs/fiscal/material-base.md` (ou caminho real do material didático)?** Não existe `docs/fiscal/` nem `AGENTS.md` no HEAD. Sem o anexo, a Etapa 2 segue só pelo código + fontes oficiais; se o material contiver regras de negócio esperadas (CFOPs, regimes, operações prioritárias), informe o caminho para confrontar antes da modelagem.
2. **Onde está o frontend (repositório/branch)?** Nenhuma UI neste repo — confirmar se as telas das Etapas 4/10 devem ser APIs + contratos aqui ou implementação em outro repositório (evita UI duplicada e escolha de framework sem contexto).

> **Atualização 19/09/2026 (pós-Etapa 1, sem alterar o diagnóstico):** (1) material recebido e gravado em `docs/fiscal/material-base.md` (didático, exemplos não compilados); (2) usuário confirmou **sem frontend, somente backend** — Etapas 4/10 entregam APIs + contratos, sem declarar telas. Ver `STATUS.md`.
