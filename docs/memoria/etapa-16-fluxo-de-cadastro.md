# Etapa 16 — Documentação: Fluxo de Cadastro Oficial

## Objetivo

Consolidar a documentação **oficial** do estado atual da API (Etapa 15) e
estabelecer a **ordem oficial de cadastro** das entidades, retomando a pendência
da Etapa 15 (como os demais microsserviços consultam o acesso do tenant a um
módulo). **Etapa exclusivamente de documentação — nenhuma mudança de código.**

## O que foi feito

### 1. `docs/FLUXO-DE-CADASTRO.md` (novo)

Guia operacional da **ordem única suportada** de cadastro:

1. **Modules** (`POST /api/modules`, SuperAdmin — equipe SmartSync)
2. **Plans por módulo** (`POST /api/modules/{moduleId}/plans`, SuperAdmin)
3. **Tenant** (`POST /api/tenants`, SuperAdmin) — nasce **sem** plano (Etapa 15)
4. **Vínculo Tenant↔Module** (`POST /api/tenants/{tenantId}/modules`, SuperAdmin)
   — plano por módulo; troca/desvinculação preservam histórico
5. **Branches** (`POST /api/tenants/{tenantId}/branches`, SuperAdmin ou
   TenantAdmin do próprio tenant)
6. **Usuários** (`POST /api/auth/register`): primeiro TenantAdmin
   (`type=tenantadmin`, sem `tenantId`) e depois
   Manager/Seller/Delivery/Client (com `tenantId`)

Inclui diagrama Mermaid, quem executa cada passo, exemplos de payload e
checklist. Documenta que planos **diferentes por módulo são esperados** e a
regra de um vínculo ativo por (tenant, módulo).

### 2. `docs/CONTRATO-IDENTIDADE.md` (reescrito)

Substitui o contrato desatualizado (que misturava rotas planas antigas de
`/api/plans` e `/api/branches`, linhas duplicadas e conteúdo da Etapa 09/11)
por uma versão **única, limpa e fiel ao código atual**:

- Auth (register com `type`, confirmações, login/refresh/logout, SSO), Conta
  (`/api/account/*`), Tenants (sem `planId`), Branches (aninhadas), **Modules**,
  **Plans por módulo**, **Vínculos Tenant-Modules** (seção 10);
- Modelo de dados (seção 11) com `modules`/`plans.module_id`/`tenant_modules`;
- **Seção 12 — Consulta de módulos pelos demais microsserviços** (retoma a
  pendência da Etapa 15): estado atual (endpoints SuperAdmin-only, **proibida**
  consulta direta ao banco) + **contrato recomendado planejado**
  `GET /api/tenants/me/modules` (autenticado por claim `tenant_id`, retorna
  módulos ativos com `slug`/`plan`);
- Erros e códigos (400/401/403/404/409/429), exemplos `curl` completos do fluxo
  de cadastro e referências.

### 3. `README.md`

- Adicionada seção **"Documentação de apoio"** com o link do novo
  `FLUXO-DE-CADASTRO.md` e demais docs.

### 4. `docs/memoria/INDEX.md`

- Linha da Etapa 16 no histórico; pendência da Etapa 15 atualizada (opção (a)
  documentada como recomendada; implementação pendente).

## Decisões técnicas e por quê

- **`docs/` é o local oficial** dos docs do serviço: o contrato atualizado foi
  escrito em `docs/CONTRATO-IDENTIDADE.md` (o README já referenciava esse
  caminho). Foi identificado um **duplicado obsoleto na raiz**
  (`CONTRATO-IDENTIDADE.md` v1.1/Etapa 11, com endpoints inexistentes como
  `forgot-password`) — registrado como pendência para o dono decidir
  remover/atualizar.
- **Fluxo documentado, não alterado:** a ordem Module → Plan → Tenant → vínculo
  → Branch → Usuários é a já imposta pelas FKs e regras da Etapa 15; o
  documento apenas a torna explícita e operável.
- **Contrato de consulta de módulos:** a opção **(a)** (endpoint por claim
  `tenant_id`) foi escolhida como recomendada por não expor dados cross-tenant e
  não acoplar microsserviços ao banco do Identity. **Não implementada** — fica
  como pendência explícita da Etapa 17.

## Arquivos criados/alterados

- `docs/FLUXO-DE-CADASTRO.md` (novo)
- `docs/CONTRATO-IDENTIDADE.md` (reescrito)
- `docs/DEV-CREDENTIALS.md` (recriado — follow-up)
- `CONTRATO-IDENTIDADE.md` (raiz) **removido** — duplicado obsoleto v1.1
- `README.md` (seção "Documentação de apoio")
- `docs/memoria/INDEX.md` (Etapa 16)

## Comandos executados

- Nenhum (etapa de documentação; sem build/testes — nenhuma linha de código
  alterada).

## Pendências / Próximos passos

- **Implementar** `GET /api/tenants/me/modules` (contrato §12 do
  CONTRATO-IDENTIDADE.md) — Etapa 17.
- Aplicar **limites do plano** (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) no
  cadastro de filiais/usuários (herda da Etapa 15).

> **Follow-up da própria Etapa 16 (documentação):** o duplicado obsoleto
> `CONTRATO-IDENTIDADE.md` da raiz foi **removido** e `docs/DEV-CREDENTIALS.md`
> foi **recriado** (ambos antes do commit inicial no GitHub).
