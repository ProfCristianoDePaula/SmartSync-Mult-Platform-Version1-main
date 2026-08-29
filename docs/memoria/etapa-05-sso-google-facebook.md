# Etapa 05 — Login Social (SSO) Google/Facebook — Exclusivo para a role Client

## Objetivo

Permitir login social via **Google e Facebook/Meta** para a role **Client**,
além do login local já existente. O fluxo deve vincular (ou criar) um Client
**ao tenant correto**, mesmo que o provedor externo não conheça tenants.

## O que foi feito

### 1. Pacotes oficiais (gratuitos) na `Identity.Api`

- `Microsoft.AspNetCore.Authentication.Google` (10.0.10)
- `Microsoft.AspNetCore.Authentication.Facebook` (10.0.10)

### 2. Identificação do tenant durante o fluxo OAuth

- Google/Facebook **não sabem nada sobre tenants**. O tenant é carregado no
  **`state`** do fluxo: `AuthenticationProperties.Items["tenantId"]`, preenchido
  no endpoint de início e relido no callback.
- Assinatura: `GET /api/auth/external-login/{provider}?tenantId=<guid>`.

### 3. Fluxo de primeiro login × login recorrente

Implementado em `SocialAuthService` (`I/SocialAuthService`):

- **Primeiro login**: cria o `ApplicationUser` com role `Client`, `TenantId`
  do state, `EmailConfirmed = true` (o e-mail veio validado pelo provedor),
  e registra o par `(provider, providerKey)` em `AspNetUserLogins`.
- **Login recorrente**: localiza pelo `UserManager.FindByLoginAsync(provider,
  providerKey)` e reutiliza a conta existente (sem duplicar).
- **Segurança/Regras de negócio**:
  - O usuário social é **sempre** um Client de um tenant (sem `TenantId` →
    recusado; tenant inexistente → recusado).
  - Se já existir conta local com o mesmo e-mail, **rejeita** (evita takeover
    por OAuth).
  - O e-mail do provider não pode duplicar um e-mail já usado no tenant
    (exige `UserUniquenessValidator.EnsureEmailUniqueAsync` antes de criar).
  - Usuário já vinculado que perdeu a role Client → recusado.

### 4. Credenciais OAuth (criação manual, fora do código)

- Client ID/Secret **não são commitados**. Devem ser criados à mão em:
  - **Google**: Google Cloud Console → credenciais OAuth 2.0 (redirect
    `https://<host>/signin-google`).
  - **Facebook/Meta**: Meta for Developers → Login com Facebook (redirect
    `https://<host>/signin-facebook`).
- Config via seção `OAuth` (`appsettings.json` travas; credenciais reais via
  user-secrets ou env vars `OAuth__Google__ClientId`, `OAuth__Google__
  ClientSecret`, `OAuth__Facebook__...`).
- No `Program.cs`, os schemas são registrados **somente se** as credenciais
  estiverem preenchidas (falha de registo silenciosa não existe: sem credenciais
  o endpoint de início responde 400 «não suportado»).

### 5. Fluxo do cookie intermediário (Identity.External)

- Usa `IdentityConstants.ExternalScheme` (cookie de curta duração, 10 min) para
  o handshake OAuth. **Não** cria sessão de usuário: após o callback, encerra o
  cookie e emite o **JWT da plataforma** (`TokenResponse`).
- Configuração no `Program.cs`:
  - `AddGoogle`/`AddFacebook` com `SignInScheme = IdentityConstants.ExternalScheme`.
  - `AddCookie("Identity.External")` registrando o cookie intermediário.
  - O JWT usa o mesmo `SigningKeyProvider` da Etapa 03.

## Testes manuais executados

> Como as credenciais reais exigem cadastro manual (consoles Google/Meta), a
> validação automatizada do fluxo completo depende delas. O que foi validado:

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `dotnet build` da solução | ✅ 0 avisos, 0 erros (schemas + controller referenciam corretamente) |
| 2 | `GET /api/auth/external-login/google` sem credenciais | ✅ 400 «provedor não suportado» (schemas não registrados) |
| 3 | Fluxo completo com credenciais reais | ⏳ pendente (requer criação manual dos apps OAuth) |
| 4 | Callback com tenantId no state | ⏳ pendente da validação manual end-to-end |

## Decisões técnicas e por quê

- **Provider não conhece tenant** → `tenantId` vai no `state` do OAuth
  (serializado/assinado pelo handler), garantindo que volte no callback fiel.
- **Cookie intermediário vs. sessão**: a API é stateless (JWT). O cookie externo
  só existe durante o handshake e expira em 10 min — reú/excluído ao final.
- **Recusa de e-mail já existente**: evita **account takeover**: alguém com
  conta Google do e-mail X não pode se autenticar/se apoderar de uma conta local
  existente com o mesmo e-mail. Nova regra documentada.
- **Login social exclusivo Client**: consistenteflui com a regra da Etapa 03/04.

## Arquivos criados/alterados

- `src/Identity.Application/Auth/{SocialLoginRequest,ISocialAuthService}.cs`
- `src/Identity.Infrastructure/Auth/SocialAuthService.cs`
- `src/Identity.Api/Endpoints/ExternalAuthController.cs`
- `src/Identity.Api/Program.cs` (schemas Google/Facebook + cookie externo)
- `src/Identity.Api/appsettings.json` (seção `OAuth`)
- `src/Identity.Infrastructure/DependencyInjection.cs` (DI do serviço)
- `src/Identity.Api/Identity.Api.csproj` (pacotes Auth.Google/Facebook)

## Comandos executados

- `dotnet add package Microsoft.AspNetCore.Authentication.Google --version 10.0.10`
- `dotnet add package Microsoft.AspNetCore.Authentication.Facebook --version 10.0.10`
- `dotnet build Identity.slnx` → 0 avisos, 0 erros

## Pendências / Próximos passos

- **Criar credenciais reais** (Google Cloud + Meta for Developers) para testar
  end-to-end o fluxo.
- Considerar `tenantSlug` no lugar de `tenantId` no `state` (documentação da
  etapa: paramétro fica aberto; hoje o campo obrigatório é `tenantId`).
- **Etapa 06**: confirmação de e-mail (tokens nativos do Identity) + validação
  de celular (SMS).