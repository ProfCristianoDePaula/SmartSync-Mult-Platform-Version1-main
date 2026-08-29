# Contrato — Serviço de Identidade (Identity & Tenants)

> **Status:** Atualizado (Etapa 18) — revisar a cada nova etapa que altere a API.
> **Base:** OpenAPI 3.1 / Scalar (`/scalar/v1`) — fonte da verdade para payloads.
> **Roles:** `SuperAdmin` (módulos + planos + tenants + vínculos tenant↔módulo +
> branches), `TenantAdmin` (branches do próprio tenant), qualquer role autenticada
> (endpoints de conta em `/api/auth`). O `POST /api/auth/register` é **público**
> (Client autosserviço) — não há registro interno de usuários pela API ainda.

---

## Índice

- [1. Visão geral](#1-visão-geral)
- [2. Autenticação e autorização](#2-autenticação-e-autorização)
- [3. Endpoints de Auth](#3-endpoints-de-auth)
- [4. Endpoints de Conta](#4-endpoints-de-conta)
- [5. Endpoints de Usuários](#5-endpoints-de-usuários)
- [6. Endpoints de Tenants](#6-endpoints-de-tenants)
- [7. Endpoints de Branches](#7-endpoints-de-branches)
- [8. Endpoints de Modules](#8-endpoints-de-modules)
- [9. Endpoints de Plans (por módulo)](#9-endpoints-de-plans-por-módulo)
- [10. Endpoints de Vínculos Tenant-Modules](#10-endpoints-de-vínculos-tenant-modules)
- [11. Modelo de dados](#11-modelo-de-dados)
- [12. Consulta de módulos pelos demais microsserviços](#12-consulta-de-módulos-pelos-demais-microsserviços)
- [13. Erros e códigos](#13-erros-e-códigos)
- [14. Exemplos (curl)](#14-exemplos-curl)
- [15. Referências](#15-referências)

---

## 1. Visão geral

O serviço de **Identity & Tenants** é o primeiro microsserviço da plataforma.
Ele concentra:

- o **catálogo global** de `Modules` (produtos contratáveis — cadastrado pela
  equipe SmartSync, **não** pelo tenant);
- o catálogo de `Plans` de **cada módulo** (todo plano pertence a um único
  módulo — Etapa 15);
- o cadastro de `Tenants` e seus **vínculos** com módulos/planos
  (`tenant_modules`, com histórico de vigências);
- o cadastro de **Filiais (Branches)** escopado ao tenant;
- toda a **Identidade/Autenticação/Autorização** consumida via JWT pelos demais
  serviços.

A **ordem oficial de cadastro** está documentada em
[FLUXO-DE-CADASTRO.md](FLUXO-DE-CADASTRO.md): Module → Plan por Module → Tenant
→ vínculo Tenant-Module(+Plan) → Branch → Usuários.

> **Mudanças da Etapa 15 em relação às Etapas 12/13:** `Tenant` não tem mais
> `planId`; `PlansController` deixou de ser flat (`/api/plans`) e virou aninhado
> em `/api/modules/{moduleId}/plans`; a contratação passou a ser feita via
> vínculos em `/api/tenants/{tenantId}/modules`.

## 2. Autenticação e autorização

### 2.1 Login

- `POST /api/auth/login` — login com `identifier`/`password`, retorna
  `accessToken` + `refreshToken`.

### 2.2 JWT

- Claims: `user_id`, `tenant_id`, `role`, `full_name`, `email`,
  `email_confirmed`, `phone_confirmed`, `profile_complete`.
- **`profile_complete`** (`"true"`/`"false"`, Etapa 22): cadastro do Client
  completo (documento CPF/CNPJ + nome preenchidos). Usada no onboarding —
  orienta o frontend a rotear para a etapa de completar o cadastro.
- **`tenant_id` presente apenas quando o usuário tem `TenantId`** (SuperAdmin
  global e TenantAdmin global sem vínculo não têm a claim).
- **Autorização por claim (Etapa 14):** usuário COM claim `tenant_id` só opera
  no próprio tenant (o `{tenantId}` da rota deve bater com a claim, senão 403).
  SuperAdmin SEM claim opera em qualquer tenant.
- Endpoints de tenant (CRUD de tenants, módulos, planos e vínculos) por
  enquanto são **SuperAdmin-only**.

### 2.3 Roles

| Role | Acesso |
|------|--------|
| `SuperAdmin` | Tudo (módulos, planos, tenants, vínculos tenant↔módulo, branches) |
| `TenantAdmin` | Só o próprio tenant (branches) |
| `Manager` | Reservado (futuro) |
| `Seller` | Reservado (futuro) |
| `Delivery` | Reservado (futuro) |
| `Client` | Própria conta + recovery/confirmation + SSO |

> O `POST /api/auth/register` é **público** e cria sempre um `Client`; não há
> registro interno de usuários (SuperAdmin/TenantAdmin) via API ainda.

### 2.4 Authorization header

- `Authorization: Bearer <token>` em todos os endpoints autenticados.

## 3. Endpoints de Auth

### 3.1 Cadastro (registro local)

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/auth/register` | **Pública** | Autosserviço: cria um **Client** do tenant (e-mail não confirmado) |

- Cria **sempre um `Client`** (não há campo `type`; gestão de usuários internos
  é pendência registrada — Etapa 11/16).
- `tenantId` é **obrigatório**; a conta só loga após confirmar o e-mail.
- Unicidade de `document` (CPF) e `email` **por tenant** (Etapa 04).

Payload (`RegisterRequest`): `fullName`, `email`, `password`, `tenantId`,
`document?`. Resposta `201` (sem tokens).

### 3.2 Confirmação de e-mail e celular

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/auth/resend-confirmation-email` | Pública | Reenvia o link de confirmação (resposta neutra) |
| POST | `/api/auth/confirm-email` | Pública | Confirma o e-mail com token |
| POST | `/api/auth/send-sms-code` | Autenticada | Envia código SMS (Client) |
| POST | `/api/auth/confirm-phone` | Autenticada | Confirma o celular com o código (Client) |

### 3.3 Login, tokens e gestão de conta

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/auth/login` | Pública | Login local (`identifier`/`password`), retorna `accessToken`/`refreshToken` |
| POST | `/api/auth/refresh-token` | Pública | Renova o par de tokens com `refreshToken` |
| POST | `/api/auth/forgot-password` | Pública | Envia link de redefinição de senha por e-mail |
| POST | `/api/auth/forgot-password/sms` | Pública | Envia código de redefinição (6 dígitos) por SMS |
| POST | `/api/auth/reset-password` | Pública | Efetiva a troca (token do e-mail ou código SMS) |
| POST | `/api/auth/change-password` | Autenticada | Troca a senha (valida a atual; revoga as demais sessões) |
| POST | `/api/auth/logout` | Autenticada | Revoga o refresh token da sessão atual |
| POST | `/api/auth/logout-all` | Autenticada | Revoga todos os refresh tokens do usuário |
| POST | `/api/auth/profile` | Autenticada (Client) | Completa o cadastro (documento obrigatório, nome opcional) e **reemite tokens** com `profile_complete=true` |
| GET | `/api/auth/jwks` | Pública | Chave pública (JWKS) para validar os access tokens |
| GET | `/api/roles` | Autenticada | Catálogo de roles da plataforma |

### 3.4 SSO (Client)

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| GET | `/api/auth/external-login/{provider}?tenantId=<guid>` | Pública | Inicia login social (Google/Facebook) já sabendo o tenant |
| GET | `/api/auth/external-login-callback` | Pública | Callback do provedor (emite o JWT da plataforma) |

> **Erros do callback (Etapa 21):** os fracassos são diferenciados por código
> HTTP — **400** para request/tenant inválido (tenantId ausente/inexistente,
> provider não suportado, e-mail/sub ausente do IdP), **401** para conflito real
> de e-mail (o e-mail já existe na plataforma mas nunca foi vinculado a esse
> provedor — anti-takeover; mensagem "já existe uma conta com este e-mail
> cadastrada de outra forma") e **403** quando a conta vinculada ao provedor não
> possui a role Client. O login recorrente (mesmo usuário, mesmo provedor)
> reutiliza a conta existente sem recriar nada.

> **Onboarding pós-login social (Etapa 22):** o `TokenResponse` emitido pelo
> callback (e pelo login/refresh locais) traz o campo
> **`requiresProfileCompletion`** (`true` quando o Client ainda não tem o
> documento do cadastro preenchido). O frontend usa isso para rotear para o
> `POST /api/auth/profile` (seção 3.3), que reemite o par de tokens com a claim
> `profile_complete=true`.

## 4. Endpoints de Conta

> **Não há rota `/api/account/*` nem `GET /api/account/me`.** As ações de conta
> (troca de senha, logout/logout-all, confirmação de e-mail/celular e completar
> o cadastro — `POST /api/auth/profile`) vivem sob `/api/auth` (ver seções 3.2 e
> 3.3). Um endpoint de leitura do "perfil do usuário autenticado"
> (`GET /api/account/me`) **não está implementado**.

## 5. Endpoints de Usuários

> A gestão dedicada de usuários (listar/editar e criar usuários internos —
> TenantAdmin/Manager/Seller/Delivery) ainda **não existe** como feature. O único
> registro via API é o **Client autosserviço** (`POST /api/auth/register`,
> seção 3.1), público, com unicidade de `email`/`document` **por tenant**
> (Etapa 04). Usuários internos são criados por seed/infraestrutura.

## 6. Endpoints de Tenants

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/tenants` | `SuperAdmin` | Cria tenant (pessoa física ou jurídica; documento CPF/CNPJ e e-mail únicos globais; nasce **sem** plano/módulo) |
| GET | `/api/tenants` | `SuperAdmin` | Lista paginada (`page`, `pageSize`, `status`, `search`, `documento`) |
| GET | `/api/tenants/{id}` | `SuperAdmin` | Detalhe |
| PUT | `/api/tenants/{id}` | `SuperAdmin` | Atualiza (`legalName`, `tradeName`, `email`, `status`; documento CPF/CNPJ imutável e fora do payload) |
| DELETE | `/api/tenants/{id}` | `SuperAdmin` | Soft delete (documento/e-mail liberados) |

Payload de criação (`CreateTenantCommand`): `legalName`, `tradeName`,
`tipoPessoa` (**inteiro**: `1` = Fisica, `2` = Juridica — a API não aceita a
string `"Fisica"/"Juridica"`), `documento` (CPF 11 / CNPJ 14 dígitos, com ou
sem máscara), `email`. **Não há mais `planId`** (Etapa 15) — a contratação é
feita pelos vínculos (seção 10). A unicidade do `documento` vale para o
número, independente do tipo de pessoa (Etapa 17).

Payload de edição (`UpdateTenantCommand`): `legalName`, `tradeName`, `email`,
`status` (inteiro: `1` = Active, `2` = Inactive, `3` = Suspended). O `Id` vem
da rota; `tipoPessoa`/`documento` não são editáveis.

## 7. Endpoints de Branches

Sempre aninhados em um tenant (Etapa 14). Queries escopadas por `TenantId` —
listagem/detalhe nunca vazam filiais de outro tenant.

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/tenants/{tenantId}/branches` | `SuperAdmin`, `TenantAdmin` | Cria branch do tenant (404 se tenant não existe) |
| GET | `/api/tenants/{tenantId}/branches` | `SuperAdmin`, `TenantAdmin` | Lista paginada (`page`, `pageSize`, `includeInactive`) |
| GET | `/api/tenants/{tenantId}/branches/{branchId}` | `SuperAdmin`, `TenantAdmin` | Detalhe |
| PUT | `/api/tenants/{tenantId}/branches/{branchId}` | `SuperAdmin`, `TenantAdmin` | Edita |
| DELETE | `/api/tenants/{tenantId}/branches/{branchId}` | `SuperAdmin`, `TenantAdmin` | Soft delete |

Payload de criação (`CreateBranchCommand`): `name`, `address` { `street`,
`number`, `complement?`, `district`, `city`, `state` (UF), `postalCode` },
`contact` { `phone`, `secondaryPhone?`, `email?` }. VOs validados: telefone
10/11 números, CEP 8 dígitos (campo `postalCode`), UF 2 letras.

## 8. Endpoints de Modules

Catálogo global de módulos — **exclusivo SuperAdmin** (equipe SmartSync).

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/modules` | `SuperAdmin` | Cria módulo (`name`, `slug`, `description?`) — `slug` imutável |
| GET | `/api/modules` | `SuperAdmin` | Lista paginada (`page`, `pageSize`, `includeInactive`) |
| GET | `/api/modules/{id}` | `SuperAdmin` | Detalhe |
| PUT | `/api/modules/{id}` | `SuperAdmin` | Edita `name`/`description` (**slug não muda**) |
| DELETE | `/api/modules/{id}` | `SuperAdmin` | Soft delete |

- `slug`: regex `^[a-z0-9]+(-[a-z0-9]+)*$` (ex.: `agro`, `ecommerce`,
  `agenda`). É o **identificador estável** usado pelos demais microsserviços.
- `name` e `slug` únicos entre módulos **ativos** (índice parcial
  `WHERE is_active`); soft delete libera ambos.

## 9. Endpoints de Plans (por módulo)

Catálogo de planos **aninhado no módulo** (Etapa 15) — **exclusivo SuperAdmin**.
O `{moduleId}` da rota é sempre imposto: a API nunca atravessa módulos.

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/modules/{moduleId}/plans` | `SuperAdmin` | Cria plano do módulo (404 se módulo inexistente/inativo) |
| GET | `/api/modules/{moduleId}/plans` | `SuperAdmin` | Lista paginada (`page`, `pageSize`, `includeInactive`) |
| GET | `/api/modules/{moduleId}/plans/{id}` | `SuperAdmin` | Detalhe (404 se plano de outro módulo) |
| PUT | `/api/modules/{moduleId}/plans/{id}` | `SuperAdmin` | Edita (404 se plano de outro módulo) |
| DELETE | `/api/modules/{moduleId}/plans/{id}` | `SuperAdmin` | Soft delete |

- Payload de criação (`CreatePlanCommand`): `name`, `description?`,
  `monthlyPrice`, `annualPrice`, `trialDays`, `features` (lista), `maxBranches`,
  `maxUsers`, `maxStorageMb`.
- **Nome único por módulo** entre planos ativos (índice parcial composto
  `(module_id, name) WHERE is_active`) — o mesmo nome pode existir em módulos
  diferentes.
- Soft delete oculta o plano das consultas e libera o nome no módulo.

## 10. Endpoints de Vínculos Tenant-Modules

Contratação do tenant: vínculo com **vigência** (histórico preservado para
billing). **Exclusivo SuperAdmin** (decisão da Etapa 15).

| Método | Rota | Roles | Descrição |
|--------|------|-------|-----------|
| POST | `/api/tenants/{tenantId}/modules` | `SuperAdmin` | Vincula tenant a módulo com um plano (404 se tenant inexistente; 400 se módulo/plano inválido ou vínculo ativo já existe) |
| PUT | `/api/tenants/{tenantId}/modules/{moduleId}` | `SuperAdmin` | Troca o plano do vínculo ativo (mesmo plano = idempotente; sem vínculo ativo = reativa/cria) |
| DELETE | `/api/tenants/{tenantId}/modules/{moduleId}` | `SuperAdmin` | Encerra o vínculo ativo (404 se não houver) — histórico preservado |
| GET | `/api/tenants/{tenantId}/modules` | `SuperAdmin` | Lista vínculos do tenant (padrão: só ativos; `includeInactive` traz histórico) |

- Regras:
  - **No máximo UM vínculo ativo** por (tenant, módulo) — índice único parcial
    `(tenant_id, module_id) WHERE status = 1` + verificação na aplicação.
  - O `planId` precisa ser **ativo e pertencer ao módulo** do vínculo (400 caso
    contrário).
  - Trocar plano encerra a vigência atual (`endDateUtc`) e abre uma nova; mesmo
    plano devolve o vínculo atual; sem vínculo ativo, cria um novo.
  - Desvincular encerra a vigência ativa (não remove o histórico).
  - O nome do `module` é retornado no DTO (mesmo soft-deletado); o `plan` é
    referenciado apenas por `planId` (FK) — o nome é obtido na tabela de planos.

Payloads: `LinkTenantModuleCommand` / `UpdateTenantModuleCommand` com
`{ moduleId, planId }` (`tenantId` vem da rota). Resposta `TenantModuleDto`:
`id`, `tenantId`, `moduleId`, `moduleName`, `planId`, `status`
(`Active=1`/`Inactive=2`), `startDateUtc`, `endDateUtc?`, `createdAtUtc`.

## 11. Modelo de dados

Tabelas principais (Postgres):

| Tabela | Notas |
|--------|-------|
| `users` / `AspNetRoles` / `AspNetUserRoles` (Identity) | `ApplicationUser` (`TenantId?`, `FullName`, `Document`); índices únicos `(tenant_id, email)` e `(tenant_id, document)` |
| `tenants` | documento (CPF/CNPJ: `tipo_pessoa` + `documento`) e e-mail únicos globais; soft delete (`is_active`, `deleted_at_utc`); **sem** `plan_id` (Etapa 15) |
| `branches` | FK `tenant_id`; soft delete; nome único por tenant entre ativas |
| `modules` | Catálogo global; `slug` único entre ativos (imutável); soft delete |
| `plans` | FK **obrigatória** `module_id` (Restrict); nome único por módulo entre ativos; soft delete |
| `tenant_modules` | FK `tenant_id`/`module_id`/`plan_id` (Restrict); `status` (`Active=1`/`Inactive=2`); `start_date_utc`/`end_date_utc`; índice único parcial `(tenant_id, module_id) WHERE status = 1` |

IDs são `uuid` (record structs fortes: `TenantId`, `BranchId`, `ModuleId`,
`PlanId`, `TenantModuleId`). VOs validados no construtor: `Documento`
(CPF/CNPJ por `TipoPessoa`), `Cnpj`, `Cpf`, `Email`, `Address`, `Contact`.

## 12. Consulta de módulos pelos demais microsserviços

### 12.1 Estado atual

- Os endpoints de vínculo (seção 10) são **SuperAdmin-only** e exigem
  `{tenantId}` na rota — **não servem** para um microsserviço validar o acesso
  de um tenant em runtime, pois ele não possui token de SuperAdmin.
- Para isso existe o endpoint **`GET /api/tenants/me/modules`** (§12.2, abaixo),
  autenticado por claim `tenant_id` e disponível para qualquer role do tenant.
  **Consultar o banco do Identity diretamente é proibido** (acoplamento; o
  Identity é dono dos dados).

### 12.2 Contrato implementado (Etapa 20)

Para as features de negócio (SmartSync Agro, E-Commerce, Agenda etc.), o padrão é
o endpoint **autenticado por claim** (qualquer role do tenant):

```
GET /api/tenants/me/modules
Authorization: Bearer <token com claim tenant_id>
```

- **Qualquer role autenticada COM a claim `tenant_id`** (TenantAdmin, Client,
  Manager etc.). Usuário **sem** a claim (ex.: SuperAdmin global) recebe
  **`403`** — não há "meu tenant".
- Deriva o `TenantId` da **claim** `tenant_id` (nunca de query param) — não há
  risco de vazar o catálogo de outro tenant.
- Retorna só vínculos **ativos** (`status = Active`) e módulos não
  soft-deletados; o `slug` do módulo é o identificador estável para o consumo e
  o `plan` é o **nome** do plano (para aplicar limites `maxBranches`,
  `maxUsers`, `maxStorageMb`).

Resposta `200` — lista de módulos ativos do tenant do token:

```json
{
  "items": [
    {
      "module": "agro",
      "name": "SmartSync Agro",
      "plan": "Essencial",
      "status": "active",
      "startDateUtc": "2026-08-09T22:40:00Z",
      "endDateUtc": null
    }
  ]
}
```

- Alternativa administrativa (integrações de billing, fora do runtime do
  cliente): reutilizar `GET /api/tenants/{tenantId}/modules` (SuperAdmin).
- **Status:** **implementado (Etapa 20)**.

## 13. Erros e códigos

Formato padrão: `ProblemDetails` (`application/problem+json`).

| Código | Significado |
|--------|-------------|
| `400` | Payload inválido / regra de negócio violada (ex.: plano de outro módulo; vínculo ativo já existente; **CNPJ/CPF inválido; documento/e-mail já em uso**) |
| `401` | Sem token, token inválido/expirado, ou **credenciais incorretas / e-mail não confirmado** no login |
| `403` | Role sem permissão, ou `{tenantId}` da rota ≠ claim `tenant_id` |
| `404` | Recurso inexistente (ex.: tenant; branch fora do tenant; plano de outro módulo) |
| `409` | Violação de unicidade capturada direto do banco (`DbUpdateException` em índices únicos) — quando tratada na aplicação a API responde `400` |
| `429` | Rate limit de `login`/`refresh`/`register`/recuperação excedido |

> Violações capturadas direto do banco (`DbUpdateException` em índices únicos)
> também retornam `409`.

## 14. Exemplos (curl)

> Ambiente de dev: <http://localhost:8080>. Credenciais padrão em
> [DEV-CREDENTIALS.md](DEV-CREDENTIALS.md).

### 14.1 Login (SuperAdmin de bootstrap)

```bash
curl -s -X POST http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"identifier":"sa@smartsync.com.br","password":"SUA_SENHA_DO_ENV"}'
```

Guarde o `accessToken` em `TOKEN`.

### 14.2 Criar módulo

```bash
curl -s -X POST http://localhost:8080/api/modules \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"SmartSync Agro","slug":"agro","description":"Gestão rural"}'
```

### 14.3 Criar plano do módulo

```bash
curl -s -X POST http://localhost:8080/api/modules/$MODULE_ID/plans \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Essencial","monthlyPrice":49.90,"annualPrice":499.00,"trialDays":7,
       "features":["Dashboard","Relatórios"],"maxBranches":5,"maxUsers":10,"maxStorageMb":1024}'
```

### 14.4 Criar tenant

```bash
curl -s -X POST http://localhost:8080/api/tenants \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"legalName":"Fazenda Boa Vista LTDA","tradeName":"Boa Vista",
       "tipoPessoa":2,"documento":"11222333000181",
       "email":"contato@boavista.com.br"}'
```

Pessoa física: `"tipoPessoa":1,"documento":"12345678909"` (CPF com 11 dígitos).
> `tipoPessoa` é **inteiro** (1 = Fisica, 2 = Juridica); strings `"Fisica"`/
> `"Juridica"` retornam `400`.

### 14.5 Vincular módulo (contratação)

```bash
curl -s -X POST http://localhost:8080/api/tenants/$TENANT_ID/modules \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"moduleId\":\"$MODULE_ID\",\"planId\":\"$PLAN_ID\"}"
```

### 14.6 Criar branch do tenant

```bash
curl -s -X POST http://localhost:8080/api/tenants/$TENANT_ID/branches \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Filial Centro",
       "address":{"street":"Rua das Flores","number":"123","district":"Centro",
                  "city":"São Paulo","state":"SP","postalCode":"01310100"},
       "contact":{"phone":"11999991234"}}'
```

### 14.7 Registrar Client (autosserviço público)

```bash
curl -s -X POST http://localhost:8080/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"fullName":"Carlos Cliente","email":"carlos@boavista.com.br",
       "password":"Senha!2026x","tenantId":"'$TENANT_ID'","document":"98765432100"}'
```

> Autosserviço **público** (sem token): cria um Client com e-mail não confirmado;
> confirme o e-mail (`POST /api/auth/confirm-email`) antes do login. Usuários
> internos (TenantAdmin/Manager/Seller/Delivery) ainda não são criados pela API.

## 15. Referências

- [README.md](../README.md) — guia para subir o sistema e rodar os testes.
- [FLUXO-DE-CADASTRO.md](FLUXO-DE-CADASTRO.md) — ordem oficial de cadastro das
  entidades (Module → Plan → Tenant → vínculo → Branch → Usuários).
- [DEV-CREDENTIALS.md](DEV-CREDENTIALS.md) — credenciais de desenvolvimento e
  chamadas prontas.
- [CONFIGURACAO-CREDENCIAIS.md](CONFIGURACAO-CREDENCIAIS.md) — credenciais
  externas (Google, Facebook, Twilio, Gmail).
- [docs/memoria/](memoria/) — histórico e decisões por etapa
  (Etapas 00–16).
- Scalar (dev): <http://localhost:8080/scalar/v1>; OpenAPI:
  `GET /openapi/v1.json`.
