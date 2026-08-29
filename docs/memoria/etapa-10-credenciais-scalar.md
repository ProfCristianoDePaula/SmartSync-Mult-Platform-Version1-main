# Etapa 10 — Credenciais Externas & Troca do Swagger pelo Scalar

## Objetivo

1. Preparar o **ambiente** para você (dono) conseguir configurar sozinho as
   credenciais externas que o serviço depende: **Google OAuth, Facebook/Meta
   Login, Twilio (SMS)** e **e-mail via Gmail (SMTP/MailKit)** — tudo mapeado
   no guia `docs/CONFIGURACAO-CREDENCIAIS.md`.
2. Substituir a UI de documentação **Swagger (Swashbuckle UI)** pelo
   **Scalar** (`Scalar.AspNetCore`), compatível com o OpenAPI nativo do
   `Microsoft.AspNetCore.OpenApi` já em uso, habilitando **Bearer JWT** na UI
   em `/scalar` para testes manuais dos endpoints protegidos.

## Decisões do usuário (registradas nesta etapa)

- **Conta de e-mail de envio:** um **Gmail pessoal (@gmail.com)**.
  Fluxo: **verificação em 2 etapas ativada + Senha de App (16 caracteres)**.
  A opção Workspace (OAuth2/XOAUTH2, obrigatória desde mai/2025) foi documentada
  no guia como conteúdo, **sem travar a implementação** — se o usuário migrar,
  o `SmtpEmailSender` precisa de `SaslMechanismOAuth2` (interface não muda).
- **Provedor de SMS:** **Twilio (trial)** — já suportado pelo `TwilioSmsSender`;
  limitações do trial documentadas no código e no guia.

## O que foi feito

### 1. `docs/CONFIGURACAO-CREDENCIAIS.md` (novo)

Guia passo a passo para o usuário (não é código). Cobre:

- **Google OAuth**: Google Cloud Console → Tela de consentimento (External) →
  OAuth Client ID tipo **"Web application"**; URIs de redirecionamento
  `http://localhost:8080/signin-google` (dev) e `https://<domínio>/signin-google`
  (prod) — o caminho `/signin-google` é o default do handler
  `Microsoft.AspNetCore.Authentication.Google` (não há `CallbackPath` custom).
- **Facebook/Meta**: Facebook for Developers → app **Consumer** → produto
  "Facebook Login" → **Valid OAuth Redirect URIs** com `signin-facebook`
  (dev/prod); App ID e App Secret; modo Development para testes.
  > Lembrete: Meta exige HTTPS no redirect (dev com `http://localhost` ok).
- **Twilio**: trial → Account SID/Auth Token; número de origem (Buy a Number);
  claro sobre trial (só números verificados, marca-d'água, créditos limitados);
  `Sms:Provider = "twilio"` para ativar; `"log"` em dev.
- **Gmail pessoal**: verificação em 2 etapas + menu "Senhas de app" → senha de
  16 caracteres. SMTP padrão `smtp.gmail.com:587` STARTTLS,
  `Email:Username/Password`. (Workspace documentado como alternativa.)
- **Tabela única final**: credencial | onde obter | chave no projeto | ambiente
  — e regra: dev via `dotnet user-secrets`, prod via env vars `__`/secret manager,
  **nunca** no `appsettings.json` versionado.

### 2. Scalar no lugar do Swagger UI

- **Pacote**: removido `Swashbuckle.AspNetCore.SwaggerUI` (10.2.3) →
  adicionado `Scalar.AspNetCore` **2.16.18** (binário em `lib/net10.0`).
- **Program.cs** (apenas em Development):
  ```csharp
  app.MapOpenApi();
  if (app.Environment.IsDevelopment())
  {
      app.MapScalarApiReference(options =>
      {
          options.WithTitle("Identity API");
          options.AddPreferredSecuritySchemes(["Bearer"]);
          options.AddHttpAuthentication("Bearer", scheme => { });
      });
  }
  ```
  - `MapScalarApiReference()` serve o Scalar em `/scalar` (default) e consome o
    OpenAPI nativo (`/openapi/v1.json`).
  - O esquema **`Bearer`** usado é o que o `BearerSecuritySchemeTransformer`
    já publica em `components.securitySchemes` — o Scalar renderiza o campo de
    JWT a partir dele (a `AddHttpAuthentication("Bearer", …)` liga o prefill).
- **Documento `/openapi/v1.json`**: continua sempre exposto (consumidores);
  a UI Scalar fica só em dev (como o Swagger estava).

## Testes manuais executados

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `dotnet build Identity.slnx` | ✅ 0 erros / 0 warnings |
| 2 | `dotnet test` (Testcontainers Postgres) | ✅ 20/20 passando |
| 3 | Container up (`docker compose up -d api`, env Development) | ✅ api healthy |
| 4 | `GET /api/health` | ✅ 200 |
| 5 | `GET /scalar/v1` | ✅ 200, `<title>Identity API</title>`, fonte `openapi/v1.json`, `preferredSecurityScheme:["Bearer"]` |
| 6 | `GET /openapi/v1.json` | ✅ openapi 3.1.1; `securitySchemes.Bearer` = http/bearer/JWT |

## Decisões técnicas e por quê

- **Scalar em vez de Swagger UI**: front moderno, leve e amigável para testes
  manuais; integra com o pipeline nativo `.NET OpenAPI` (sem SwaggerGen).
- **Só trocar a UI, nada de openapi**: a spec é gerada pelo
  `Microsoft.AspNetCore.OpenApi` (desde a Etapa 09); o Scalar apenas renderiza.
- **Bearer ligado no OpenAPI + `AddAuth`**: sem esquema no documento o Scalar
  não mostra campo de token; com `securitySchemes.Bearer` no doc, o Scalar
  renderiza o input para simular os endpoints `[Authorize]`.
- **UI restrita a Dev**: /openapi/v1.json deixa a spec pública; a UI de dev
  (como era Swagger) não sobre em prod; produção não expõe a página interativa.
- **User-secrets para dev / env para prod**: regra já consolidada (Etapa 01);
  o guia explicita os comandos `dotnet user-secrets set ...` e as variáveis
  `OAuth__*`, `Sms__*`, `Email__*`.

## Arquivos criados/alterados

- `docs/CONFIGURACAO-CREDENCIAIS.md` (novo)
- `src/Identity.Api/Identity.Api.csproj` (+ Scalar.AspNetCore 2.16.18, − Swashbuckle.AspNetCore.SwaggerUI)
- `src/Identity.Api/Program.cs` (`MapScalarApiReference`, `using Scalar.AspNetCore`)

## Comandos executados

- `dotnet remove src/Identity.Api/Identity.Api.csproj package Swashbuckle.AspNetCore.SwaggerUI`
- `dotnet add src/Identity.Api/Identity.Api.csproj package Scalar.AspNetCore --version 2.16.18`
- `dotnet build Identity.slnx`
- `dotnet test tests/Identity.Tests/Identity.Tests.csproj`
- `docker compose build api && docker compose up -d api`
- Smoke: `/api/health`, `/scalar/v1`, `/openapi/v1.json`

## Pendências (dependem do usuário)

- **Obter e configurar** as credenciais externas seguindo
  `docs/CONFIGURACAO-CREDENCIAIS.md` (para dev via user-secrets).
- **Criar a conta real na Twilio** (trial): Obter SID/Auth Token/número de
  origem e verificar um número de destino para testes reais.
- **Confirmar Gmail** (já confirmado: **pessoal**) e a primeira **Senha de App**.
- End-to-end real com credenciais (testar `external-login/google`,
  `external-login/facebook`, SMS real, e-mail real) — hoje só validados os
  fluxos locais/em captura.