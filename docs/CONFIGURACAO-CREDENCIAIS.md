# Configuração de Credenciais Externas

> Guia **para você (dono do projeto)** obter e configurar as credenciais que
> este microsserviço consome. Nada aqui é código — é a cartilha de setup.
>
> Regra de ouro: **nenhuma credencial vai para `appsettings.json` versionado**.
> Em dev usamos `dotnet user-secrets`; em produção, variáveis de ambiente ou
> secret manager (docker-compose `.env` / Docker Secrets / seu secret store).

---

## 1. Google OAuth (login com Google — role Client)

Usado pelo serviço `SocialAuthService` (Etapa 05), endpoints `external-login/google`
e o callback. Fluxo de obtenção:

### 1.1 Criar credenciais no Google Cloud Console

1. Acesse **https://console.cloud.google.com** e escolha/crie um projeto.
2. Menu → **APIs e serviços** → **Tela de consentimento (OAuth consent screen)**:
   - Selecione **Externo** (External) — para apps com usuários fora do Workspace.
   - Preencha nome do app (ex.: "Identity Platform - Dev"), e-mail de suporte.
   - **Público-alvo**: adicione os e-mails de teste (dev) ou escopo público.
   - Escopo default `.../auth/userinfo.email` e `.../auth/userinfo.profile`
     (fornecido automaticamente pelo handler do ASP.NET Core).
   - Salve.
3. Menu → **APIs e serviços** → **Credenciais** → **Criar credenciais** →
   **ID de cliente OAuth**.
   - Tipo de aplicativo: **Web application**.
   - **URIs de redirecionamento autorizados** — cadastre as duas (dev + prod):
     - dev: `http://localhost:8080/signin-google` (ou a porta que a API subir)
     - prod: `https://<seu-dominio>/signin-google`
   - O caminho `/signin-google` é o **default** do middleware
     `Microsoft.AspNetCore.Authentication.Google` — não existe `CallbackPath`
     customizado no `Program.cs`. Se um dia mudar, terá que espelhar aqui.
4. Após criar, o console mostra **Client ID** e **Client Secret** (copie os dois).

> ⚠️ A porta da API em dev é a do compose: **8080** (o redirect do provider deve
> ser exatamente `http://localhost:8080/signin-google`).

### 1.2 Onde configurar ClientId/ClientSecret

Chaves de configuração usadas no `Program.cs` (seção `OAuth:Google`):

| Config | `OAuth:Google:ClientId` | `OAuth:Google:ClientSecret` |
|--------|-------------------------|-----------------------------|

- **Dev (user-secrets da `Identity.Api`):**
  ```bash
  dotnet user-secrets set "OAuth:Google:ClientId" "....apps.googleusercontent.com" --project src/Identity.Api
  dotnet user-secrets set "OAuth:Google:ClientSecret" "GOCSPX-..." --project src/Identity.Api
  ```
- **Produção (variável de ambiente / secret manager):** a variável usa `__`
  para separar níveis — `OAuth__Google__ClientId`, `OAuth__Google__ClientSecret`.
  No docker-compose, isso entra no `environment` do serviço `api` via `.env`
  (ex.: `OAuth__Google__ClientId: ${OAUTH_GOOGLE_CLIENT_ID:-}`).

> O middleware registra o schema **somente se** ambas as credenciais existirem;
> sem elas o endpoint responde 400 — comportamento já testado (Etapa 05).

---

## 2. Facebook / Meta Login (login com Facebook — role Client)

**Importante:** durante a criação, você precisará de uma **conta verificada** no
Meta e, para o app ir de "Development" a "Active", de validação/revisão caso
passe a públicos em produção (o modo Development permite testar com contas que
você definir como Admin/Tester no app).

### 2.1 Criar o app no Facebook for Developers

1. Acesse **https://developers.facebook.com/apps** → **Create App** → escolha
   **"Consumer"** (tipo do app para login de pessoas físicas).
2. Na configuração do app, adicione o produto **"Facebook Login"**.
3. Em **Facebook Login → Configurações**:
   - **Valid OAuth Redirect URIs**:
     - dev: `http://localhost:8080/signin-facebook`
     - prod: `https://<seu-dominio>/signin-facebook`
   - Salvar.
4. Anote **App ID** (= ClientId) e **App Secret** (= ClientSecret), na página
   **Configurações → Basic** do app.
5. Adicione seu conta como **Admin/Testador** do app (Roles) se quiser testar
   em Development Mode.

### 2.2 Onde configurar

| Config | `OAuth:Facebook:ClientId` | `OAuth:Facebook:ClientSecret` |
|--------|---------------------------|-------------------------------|

- **Dev (user-secrets):**
  ```bash
  dotnet user-secrets set "OAuth:Facebook:ClientId" "<App ID>" --project src/Identity.Api
  dotnet user-secrets set "OAuth:Facebook:ClientSecret" "<App Secret>" --project src/Identity.Api
  ```
- **Produção:** `OAuth__Facebook__ClientId`, `OAuth__Facebook__ClientSecret`
  (mesmo padrão de ambiente do Google).

> Lembra: o count (HTTP) no Facebook **exige HTTPS** na redirect URI. Em dev,
> use `http://localhost:8080/signin-facebook` **enquanto não houver domínio**.
> Em produção só `https`.

---

## 3. Twilio — SMS (validação de celular)

O `TwilioSmsSender` chama a **REST API da Twilio** direto (via HttpClient) com
credenciais da conta. O `ISmsSender` em produção usa a implementação Twilio
somente quando `Sms:Provider = twilio` **e** as três credenciais estão
preenchidas (`TwilioConfigured`). Caso contrário, cai no `LogSmsSender`.

### 3.1 Criar a conta trial

1. Acesse **https://www.twilio.com/try-twilio** → crie a conta (dados básicos).
2. No **console**, na home, fique com o **Account SID** e o **Auth Token**
   (segredos — o Auth Token é mostrado para escovado e pode ser regenerado).
3. Compre/adote um **número de origem** (FromNumber): menu **Phone Numbers → Buy
   a Number** (no trial o número é gratuito mas tem restrições de envio).
4. No **trial**, a Twilio:
   - só envia SMS **para números verificados** (verificar em **Verify a Phone
     Number**) e as mensagens saem com a marca-d'água "Sent from your Twilio
     trial account";
   - **só aceita templates predefinidos** — texto livre retorna o erro
     **572006** ("Trial accounts can only use predefined SMS templates"). Para
     contornar, o sender aceita o modo trial via **`Sms:BodyTemplate`**: quando
     preenchido (ex.: `"sms_2fa"`), o `Body` da mensagem passa a ser o **nome do
     template** e o conteúdo é **genérico da Twilio** (o código gerado NÃO
     aparece no SMS — útil só para validar o canal). Quando vazio, o envio usa o
     texto customizado (exige **upgrade da conta**).

### 3.2 Onde configurar

Chaves: seção `Sms` → `Provider`, `AccountSid`, `AuthToken`, `FromNumber`,
`BodyTemplate` (opcional — modo trial).

- **Dev (user-secrets) — apenas se você realmente quiser testar envio real:**
  ```bash
  dotnet user-secrets set "Sms:Provider" "twilio" --project src/Identity.Api
  dotnet user-secrets set "Sms:AccountSid" "ACxxxxxxxx..." --project src/Identity.Api
  dotnet user-secrets set "Sms:AuthToken" "xxxxxxxx" --project src/Identity.Api
  dotnet user-secrets set "Sms:FromNumber" "+55..." --project src/Identity.Api
  dotnet user-secrets set "Sms:BodyTemplate" "sms_2fa" --project src/Identity.Api   # trial; remova em conta paga
  ```
  Com o trial, coloque seu próprio número em **Verified** e use-o como destino
  dos testes (e lembre da restrição de template acima). Em dev você também pode
  manter `Sms:Provider = "log"` e só ver o código no log.
- **Docker/produção:** `Sms__Provider`, `Sms__AccountSid`, `Sms__AuthToken`,
  `Sms__FromNumber`, `Sms__BodyTemplate` — no `docker-compose.yml` o serviço
  `api` mapeia a partir do `.env`: `SMS_PROVIDER`, `SMS_ACCOUNT_SID`,
  `SMS_AUTH_TOKEN`, `SMS_FROM_NUMBER`, `SMS_BODY_TEMPLATE`.

> Deixe `Sms:Provider` vazio ou `"log"` em dev quando não quiser gastar/no
> números verificados — nenhuma credencial é obrigatória para o sistema rodar.

---

## 4. E-mail transacional via SMTP (Gmail)

O envio usa **MailKit** (`SmtpEmailSender`) quando `Email:SmtpHost` está
preenchido (`Email:EnableSmtp`). Configuração **padrão** do Gmail no projeto:
`Email:SmtpHost = smtp.gmail.com`, `Email:SmtpPort = 587`, `Email:UseSsl = false`
(a porta **587 usa STARTTLS**; `UseSsl=true` na 587 tenta SSL direto e falha),
autenticação `Email:Username` = endereço Gmail do remetente e `Email:Password`.

> Você confirmou que a conta de envio será **um Gmail pessoal (@gmail.com)**.
> O fluxo abaixo é esse. Se um dia a conta passar a ser Google Workspace, o fluxo
> muda (seção 4.3).

### 4.1 Gmail pessoal — verificação em 2 etapas + Senha de App

Primeiro, garanta que o Gmail de envio tenha **verificação em duas etapas (2FA)
ativa** — sem isso o Google NÃO oferece o menu "Senhas de app".

1. Acesse **https://myaccount.google.com** no navegador onde o dono do Gmail
   está logado.
2. **Segurança → Verificação em duas etapas** → ative.
3. Volte para **Segurança → ⚠ Senhas de app** (aparece só com a 2FA ativa) →
   crie uma senha de app para o "MailKit / SMTP".
4. O Google gera uma **senha de 16 caracteres** (ex.: `abcd efgh ijkl mnop`).
   **Essa é a `Email:Password`** — NÃO é a senha normal da conta.

### 4.2 Configurar

| Chave | Valor |
|-------|-------|
| `Email:From` | `seugmail@gmail.com` (endereço de envio) |
| `Email:FromName` | ex.: `Identity Service` |
| `Email:SmtpHost` | `smtp.gmail.com` |
| `Email:SmtpPort` | `587` |
| `Email:UseSsl` | `false` (587 = STARTTLS; `true` só na porta 465) |
| `Email:Username` | `seugmail@gmail.com` |
| `Email:Password` | a senha de app (16 caracteres) |

**Dev (user-secrets):**
```bash
dotnet user-secrets set "Email:From" "seu@gmail.com" --project src/Identity.Api
dotnet user-secrets set "Email:SmtpHost" "smtp.gmail.com" --project src/Identity.Api
dotnet user-secrets set "Email:SmtpPort" "587" --project src/Identity.Api
dotnet user-secrets set "Email:UseSsl" "false" --project src/Identity.Api
dotnet user-secrets set "Email:Username" "seu@gmail.com" --project src/Identity.Api
dotnet user-secrets set "Email:Password" "abcd efgh ijkl mnop" --project src/Identity.Api
```
> Em dev sem SMTP configurada, o `Email:SmtpHost` vazio ativa o `LogEmailSender`
> (só loga) — nada é enviado.

**Produção:** `Email__From`, `Email__SmtpHost`, `Email__SmtpPort`,
`Email__UseSsl`, `Email__Username`, `Email__Password` (convenção `__`).

> ⚠️ Limitações Gmail pessoal: **≈ 500 e-mails/dia** e **taxa de envio baixa**
> — suficiente para confirmação de e-mail num projeto em aprendizado; para
> volume real, avaliar o Workspace com OAuth2, SendGrid/Mailgun etc.

### 4.3 Se a conta fosse Workspace (documentado, NÃO ativo agora)

Desde **maio/2025** o Google Workspace bloqueia login de terceiros com
usuário/senha para SMTP. É necessário **OAuth2 (XOAUTH2)** via MailKit:
1. Criar projeto no Google Cloud Console com escopo de SMTP (`SCOPES: SMTP AUTH`
   / `...mob/dev`):
   - habilitar Gmail API, criar OAuth Client ID do tipo "Web application"
     (ou Desktop) e manifest "Google Workspace" para SMTP AUTH.
2. Gerar refresh token (fluxo de consentimento com a conta do Workspace).
3. No `SmtpEmailSender`, usar `SaslMechanismOAuth2` do MailKit com o refresh
   token (e o ClientId/Secret) em vez de usuário/senha simples.

Se você migrar para Workspace, me avise — a implementação do sender ganha um
fluxo OAuth2 (a interface `IEmailSender` não muda, só o `SmtpEmailSender`).

---

## 5. Tabela consolidada de credenciais

| Credencial | Onde obter | Chave no projeto (`seção` ou `SEÇÃO__CAMPO`) | Ambiente |
|---|---|---|---|
| Google Client ID | Google Cloud Console → Credenciais → OAuth Client ID (Web app) | `OAuth:Google:ClientId` | dev (user-secrets) / prod (env) |
| Google Client Secret | mesmo cadastro do Client ID | `OAuth:Google:ClientSecret` | dev (user-secrets) / prod (env) |
| Facebook App ID (ClientId) | Facebook for Developers → Config → App | `OAuth:Facebook:ClientId` | dev (user-secrets) / prod (env) |
| Facebook App Secret | Facebook for Developers → Config → App | `OAuth:Facebook:ClientSecret` | dev (user-secrets) / prod (env) |
| Twilio Account SID | Console Twilio → dashboard | `Sms:AccountSid` | dev (user-secrets) / prod (env `SMS_ACCOUNT_SID`) |
| Twilio Auth Token | Console Twilio → dashboard | `Sms:AuthToken` | dev (user-secrets) / prod (env `SMS_AUTH_TOKEN`) |
| Twilio From Number | Twilio → Phone Numbers → Buy a Number | `Sms:FromNumber` | dev (user-secrets) / prod (env `SMS_FROM_NUMBER`) |
| Twilio Provider switch | `"twilio"` para ativar | `Sms:Provider` | dev (user-secrets) / prod (env `SMS_PROVIDER`) |
| Gmail remetente (From) | sua conta Gmail | `Email:From` | dev (user-secrets) / prod (env `EMAIL_FROM`) |
| Gmail host | fixo `smtp.gmail.com` no guide | `Email:SmtpHost` (587/STARTTLS) | dev (user-secrets) / prod (env `EMAIL_SMTP_HOST`) |
| Gmail Username | seu e-mail Gmail | `Email:Username` | dev (user-secrets) / prod (env `EMAIL_USERNAME`) |
| Gmail Password | "Senhas de app" do Gmail (16 chars, 2FA) | `Email:Password` | dev (user-secrets) / prod (env `EMAIL_PASSWORD`) |

> Em produção (docker-compose) as variáveis usam o separador `__`:
> `OAuth__Google__ClientId`, `Sms__AccountSid`, `Email__Password`, etc.
> A própria variável `.env` deve transportar o valor (digite no `.env` com nome
> livre, ex.: `EMAIL_SMTP_HOST`, `SMS_ACCOUNT_SID`) e o compose mapeia para a
> variável `__`.

---

## 6. Checklist rápido

- [ ] Google Cloud: tela de consentimento + OAuth Client ID (Web app) com
  `signin-google` de dev e prod cadastrados.
- [ ] Facebook for Developers: app Consumer + produto Facebook Login + URIs
  `signin-facebook` + roles de teste.
- [ ] Twilio: conta trial, Account SID/Auth Token e número de origem comprado/verificado;
      **atenção:** o trial só envia templates predefinidos (572006) — upgrade para texto livre.
- [ ] Gmail: 2FA ativo + senha de app (16 caracteres); `UseSsl=false` (587/STARTTLS).
- [ ] Dev: `dotnet user-secrets set` de todas as chaves (comando seção a seção).
- [ ] Prod: `OAuth__*`, `Sms__*`, `Email__*` no `environment` do compose e
      valores no `.env` (fora do git).
- [ ] Verificação OpenAPI/Scalar: após subir, `http://localhost:8080/scalar/v1`
      e login → colar Bearer para testar `send-sms-code`/`confirm-phone`.