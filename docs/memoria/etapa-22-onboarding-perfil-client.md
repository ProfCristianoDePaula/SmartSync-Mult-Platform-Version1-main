# Etapa 22 — Onboarding pós-login social: perfil do Client

**Data:** 10/08/2026
**Status:** Feita (final)

## Objetivo

Após o login social (Google/Facebook), o Client criado nasce com o cadastro
**incompleto** (o Google entrega e-mail/nome, mas não o documento CPF/CNPJ nem o
telefone). O frontend precisa saber disso **pela resposta da API** para rotear o
usuário para a etapa de "completar o cadastro" antes de liberar o app.

## O que foi entregue

### 1. Sinal de perfil incompleto em dois pontos do contrato

- **Claim no JWT:** `profile_complete` (`"true"`/`"false"`) — calculada em
  `ApplicationUser.ProfileComplete` (`Document` + `FullName` preenchidos) e
  emitida por `TokenService.CreateAccessTokenAsync` em **todos** os logins
  (local, social e refresh). Segue o padrão existente de
  `email_confirmed`/`phone_confirmed`.
- **Campo no `TokenResponse`:** `requiresProfileCompletion` (`bool`) — presente
  no retorno do login local, do refresh e do callback social, para o SPA
  decidir a rota sem decodificar o JWT.

### 2. Endpoint `POST /api/auth/profile` (completar cadastro)

- **Autenticado e exclusivo da role `Client`** (onboarding é do autosserviço).
- Payload (`CompleteProfileRequest`): `document` (obrigatório, CPF/CNPJ) e
  `fullName` (opcional — atualiza quando informado).
- Validações: documento obrigatório; **unicidade por tenant** (Etapa 04,
  `EnsureDocumentUniqueAsync` excluindo o próprio usuário); persistência via
  `UserManager.UpdateAsync`.
- **Reemissão de tokens:** ação sensível → revoga as sessões anteriores
  (`RevokeAllUserRefreshTokensAsync`) e devolve um **novo par** com a claim
  `profile_complete=true` (o access anterior continuaria com a claim antiga até
  expirar). Resposta `200` = `TokenResponse` novo; `400` = validação/duplicado;
  `401` sem token; `403` role ≠ Client.

### 3. Arquivos

- `src/Identity.Domain/Common/JwtClaims.cs` — claim `ProfileComplete`.
- `src/Identity.Infrastructure/Persistence/Identity/ApplicationUser.cs` —
  propriedade calculada `ProfileComplete`.
- `src/Identity.Infrastructure/Security/TokenService.cs` — claim no JWT.
- `src/Identity.Application/Auth/TokenResponse.cs` — campo
  `RequiresProfileCompletion` (propagado nos 3 emissores: `AuthService` login/
  refresh e `SocialAuthService`).
- `src/Identity.Application/Auth/CompleteProfileRequest.cs` e
  `CompleteProfileResult.cs` — novos.
- `src/Identity.Infrastructure/Auth/AuthService.cs` — `CompleteProfileAsync`.
- `src/Identity.Api/Endpoints/AuthController.cs` — `POST /api/auth/profile`.
- `tests/Identity.Tests/ProfileCompletionTests.cs` — 8 testes novos.

## Decisões de design

- **Regra de "completo" computada** (Document + FullName), não coluna explícita.
  Se o onboarding ganhar mais etapas (termos, endereço, telefone) no futuro, a
  regra evolui em um único ponto (`ApplicationUser.ProfileComplete`).
- **`EmailConflict` (401) ≠ perfil incompleto**: o anti-takeover é bloqueio de
  login; o onboarding é um estado da conta. Fluxos separados.
- **Endpoint sob `/api/auth`**, respeitando a convenção documentada de que ações
  de conta vivem em `/api/auth` (não há `/api/account/*`).

## Validação

- `scripts/run-tests-in-docker.ps1` — suíte completa **143/143** (135 anteriores
  + 8 novos). Os testes cobrem: claim e campo no 1º login social
  (`profile_complete=false`), login local com documento (`false`), completar
  perfil via HTTP (200, claim atualizada, refresh antigo revogado, novo válido,
  persistência no banco), documento obrigatório (400), duplicado por tenant
  (400), mesmo CPF em outro tenant (200), sem token (401) e role não-Client
  (403).

## Pendências / Próximos passos

- **Facebook** segue sem credenciais reais (console Meta) para validação
  end-to-end; fluxo social coberto pelos testes diretos no serviço.
- O frontend (SPA) deve ler `requiresProfileCompletion` no callback/login e
  rotear para a tela de completar cadastro, chamando `POST /api/auth/profile`
  e substituindo o par de tokens pela resposta.
