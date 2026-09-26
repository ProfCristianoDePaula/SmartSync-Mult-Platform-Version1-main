# AGENTS.md — SmartSync (inclui REGRAS FIXAS do Módulo Fiscal, 19/09/2026)

> Este arquivo foi criado na Etapa Fiscal-0 (Etapa 35). O bloco REGRAS FIXAS abaixo tem precedência para todo o trabalho do módulo Fiscal (NF-e mod. 55, NFC-e mod. 65, NFS-e). Data-base da pesquisa do roteiro: 19/09/2026 — toda regra fiscal, URL e versão de schema deve ser reconfirmada na fonte oficial antes de ir para produção.

```text
CONTEXTO
Plataforma multi-tenant SmartSync (.NET 10, Clean Architecture + DDD, PostgreSQL, Docker).
- Identity & Tenants: JWT RS256/JWKS, roles SuperAdmin/TenantAdmin/Manager/Seller/Delivery/Client, Tenants (CPF ou CNPJ), Branches, módulos por tenant (GET /api/tenants/me/modules).
- Estoque: catálogo de produtos por tenant, saldos por filial, Pedidos/Vendas (cupons, carrinho, vendas, formas de pagamento).
- Estamos criando o módulo Fiscal (NF-e mod. 55, NFC-e mod. 65, NFS-e) seguindo o mesmo padrão.

REGRAS
R1  Antes de qualquer trabalho leia docs/memoria/INDEX.md, docs/fiscal/* e as memórias relevantes. Ao final crie docs/memoria/etapa-NN-nome.md e atualize o INDEX.md (tabela + decisões + pendências).
R2  Reaproveite o que existe. Nunca duplique cadastros de Tenant, Filial, Cliente ou Produto. Mudanças em Identity/Estoque: apenas aditivas, com migration defensiva, e listadas na memória da etapa.
R3  NÃO invente: URLs de SEFAZ/prefeituras, códigos de rejeição, alíquotas, versões de schema, regras de prazo. Sem fonte oficial => marque verificado=false e registre em docs/fiscal/PENDENCIAS.md.
R4  Segredos: PFX, senha do certificado, CSC e chaves nunca em git, logs, exceções, traces, respostas de API ou testes versionados. Cifrados em repouso. Senha nunca é devolvida por endpoint algum.
R5  Multi-tenant: TenantId sempre vem da claim do JWT, nunca do body. SuperAdmin (sem tenant_id) recebe 403 em rotas operacionais. Todo teste de rota operacional inclui prova de não-vazamento entre tenants.
R6  HTTP 200 NÃO é autorização fiscal. Timeout/falha de rede após o envio => status ResultadoDesconhecido => consultar antes de qualquer reenvio. Proibido reenvio cego.
R7  Numeração e idempotência são garantidas NO BANCO (UPDATE atômico + índice único). Proibido "SELECT max()+1" ou "SELECT depois INSERT" sem restrição única.
R8  Nunca desabilitar a validação do certificado TLS do servidor. XML: DtdProcessing.Prohibit, XmlResolver=null, schemas XSD carregados de pacote local confiável. URLs de envio vêm SOMENTE do cadastro de endpoints (nunca do cliente HTTP).
R9  Documentos fiscais, eventos e protocolos jamais são apagados. Cancelar é um evento, não um DELETE.
R10 Homologação e produção são totalmente separadas (configuração, série/numeração, certificado, CSC, endpoints). Produção só com flag Fiscal:ProducaoHabilitada=true E gate de prontidão do emitente.
R11 Resultados do simulador são marcados "SIMULAÇÃO — SEM VALOR FISCAL" em qualquer saída.
R12 Cada etapa fecha com build 0 erros/0 avisos e testes verdes (xUnit + Testcontainers Postgres reais, sem mock de banco), rodados no container.
R13 Convenções do projeto: IDs tipados, ValueConverters, HasQueryFilter("Active") + índices parciais, FluentValidation -> ProblemDetails, Scalar, Serilog + CorrelationId, mensagens pt-BR, outbox para eventos.
R14 Se uma ambiguidade mudar o desenho, PARE e pergunte. Se não mudar, decida, registre no ADR e siga.
R15 Escopo: faça só o que a etapa pede. Não adiante etapas futuras.
```

## Observações locais (Etapa 35)

- Estoque expõe `8082`? Não: neste repo o `ESTOQUE_API_PORT` efetivo é `18081` (ver `docker compose ps` da Etapa 1). O Fiscal usará a porta `8082` proposta somente se livre — a confirmar na Fiscal-1 sem colidir com o mapeamento atual.
- `docs/fiscal/material-base.md` (didático, 19/09/2026) continua válido como contexto, mas as premissas P1–P7 do roteiro Fiscal prevalecem onde divergirem (ex.: microsserviço Fiscal próprio vs. bounded context no Estoque — decidido no ADR-001).
