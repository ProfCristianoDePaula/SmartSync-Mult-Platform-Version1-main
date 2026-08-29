# Etapa 06 — Confirmação de E-mail e Validação de Celular (Client)

## Objetivo

Garantir contas de Client ativas: **e-mail confirmado** (tokens nativos do
Identity) e **celular validado** (código 6 dígitos via SMS), bloqueando o
login/ações até o e-mail ser confirmado.

> **Decisão de escopo (consenso com o usuário):** NÃO criar endpoint de
> cadastro do Client nesta etapa — apenas a infraestrutura de confirmação e os
> endpoints de envio/confirmação. O cadastro em si fica para etapa futura.
> **Decisão de bloqueio:** apenas **e-mail confirmado** é obrigatório para
> login/ações por enquanto; celular é opcional (apenas informativo no JWT).

## O que foi feito

### 1. Confirmação de e-mail com tokens NATIVOS do Identity

- `AuthService.ResendConfirmationEmailAsync`: gera token com
  `GenerateEmailConfirmationTokenAsync` e envia link por e-mail. Resposta
  **neutra** sempre (`200`'se existir') para não enumerar contas.
- `AuthService.ConfirmEmailAsync`: valida com `ConfirmEmailAsync` (nativo).
- O e-mail/log usa template `Email:ConfirmationUrlTemplate` (placeholders
  `{email}`/`{token}`), apontando para o front/browser.

### 2. Validação de celular (SMS — código 6 dígitos)

- `AuthService.SendSmsCodeAsync`: gera o código com
  `GenerateChangePhoneNumberTokenAsync` e envia via `ISmsSender`.
- `AuthService.ConfirmPhoneAsync`: valida com
  `VerifyChangePhoneNumberTokenAsync` e grava com **`ChangePhoneNumberAsync`**
  (que marca `PhoneNumber` + `PhoneNumberConfirmed=true`).
  - ⚠️ **Nota de implementação:** `SetPhoneNumberAsync` NÃO marca como
    confirmado (e reseta a confirmação ao trocar o número); o método correto é
    `ChangePhoneNumberAsync`.

### 3. Senders por interface

- **`IEmailSender`** → `SmtpEmailSender` (MailKit) ou `LogEmailSender` (dev).
  Decisão por `Email:EnableSmtp` (= `SmtpHost` preenchido). Em dev sem SMTP,
  cai no `LogEmailSender` (loga o que seria enviado).
  - Sandbox dev: apontar `Email:SmtpHost` para **smtp4dev/Papercut** local
    (ex.: `127.0.0.1:25`) ou **Mailtrap** (`smtp.mailtrap.io`, credenciais do
    sandbox).
- **`ISmsSender`** → `TwilioSmsSender` (Twilio REST API via HttpClient, sem SDK
  NuGet pesado) ou `LogSmsSender`.
  - **LIMITAÇÃO documentada:** SMS transacional ilimitado e gratuito em produção
    **não existe**. O trial da Twilio envia APENAS para números verificados na
    conta, adiciona a marca d'água "Sent from your Twilio trial account", tem
    créditos limitados e expira. Para dev, `Sms:Provider` pode ser `"log"`.
  - Seleção no DI: `Sms:Provider == "twilio"` (e credenciais preenchidas) →
    Twilio; caso contrário → `LogSmsSender`.

### 4. Endpoints (`POST /api/auth/*`)

| Endpoint | Autenticação | Comportamento |
|----------|--------------|---------------|
| `resend-confirmation-email` | anônimo | resposta neutra; envia link p/ conta não confirmada que existir |
| `confirm-email` | anônimo | `ConfirmEmailAsync` (token nativo); 400 se inválido |
| `send-sms-code` | **Bearer + policy `email-confirmed`** | gera código (token nativo) e envia por SMS |
| `confirm-phone` | **Bearer + policy `email-confirmed`** | valida código e salva `PhoneNumber` confirmado |

### 5. Bloqueio de login/ações

- Login local (`AuthService.LoginAsync`) já recusava contas com
  `EmailConfirmed = false` (`SignIn.RequireConfirmedEmail = true` na
  configuração do Identity).
- **Novo:** claims `email_confirmed` e `phone_confirmed` no JWT (bool).
- **Nova policy** `email-confirmed` (`AddAuthorization`) exige
  `JwtClaims.EmailConfirmed == "true"` para os endpoints de SMS.
- Contas sociais (Etapa 05) já nascem `EmailConfirmed=true` (provedor validou).

## Testes manuais executados (Docker)

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `dotnet build Identity.slnx` | ✅ 0 avisos, 0 erros |
| 2 | Login SuperAdmin → JWT contém `email_confirmed`/`phone_confirmed` | ✅ obrigatório |
| 3 | `send-sms-code` com Bearer válido | ✅ código 6 dígitos logado (`LogSmsSender`) |
| 4 | `confirm-phone` com código certo | ✅ `PhoneNumberConfirmed = t` no banco; próximo login `phone_confirmed=true` |
| 5 | `confirm-phone` com código errado | ✅ 400 |
| 6 | `send-sms-code` dentro da policy (e-mail confirmado ativo) | ✅ 200 |
| 7 | Login do Client criado com `EmailConfirmed=false` | ✅ 401 (bloqueado) |
| 8 | `resend-confirmation-email` para e-mail que existe não confirmado → e-mail logado com link | ✅ |
| 9 | `confirm-email` com token do link | ✅ e-mail confirmado; login passa a funcionar |
| 10 | Resposta neutra para e-mail inexistente | ✅ 200 genérica |
| 11 | Confirmação dupla (reuso de token já confirmado) | ✅ realizar de novo retorna sucesso (o Identity reage como já confirmado) |

> Client/tenant de teste criados diretamente no Postgres para validar;
> credenciais reais de SMTP/Twilio pendentes (serão configuradas via
> user-secrets/env).

## Arquivos criados/alterados

- `src/Identity.Application/Notifications/{IEmailSender,ISmsSender,EmailOptions,
  SmsOptions}.cs`
- `src/Identity.Application/Auth/{IAuthService, ConfirmEmailRequest,
  ResendConfirmationEmailRequest, SendSmsCodeRequest, ConfirmPhoneRequest}.cs`
- `src/Identity.Domain/Common/JwtClaims.cs` (`email_confirmed`, `phone_confirmed`)
- `src/Identity.Infrastructure/Notifications/{SmtpEmailSender,LogEmailSender,
  TwilioSmsSender,LogSmsSender}.cs`
- `src/Identity.Infrastructure/Auth/AuthService.cs` (novos métodos)
- `src/Identity.Infrastructure/DependencyInjection.cs` (DI dos senders + token
  providers via `.AddDefaultTokenProviders()`)
- `src/Identity.Infrastructure/Identity.Infrastructure.csproj` (+ `MailKit`)
- `src/Identity.Api/Endpoints/AuthController.cs` (4 novos endpoints)
- `src/Identity.Api/Program.cs` (policy `email-confirmed`)
- `src/Identity.Api/appsettings.json` (seções `Email` e `Sms`)

## Decisões técnicas e por quê

- **Tokens nativos para confirmação**: evita reinventar; integra com
  `RequireConfirmedEmail` e com a base de `AspATUser`. O token de e-mail é
  longo (Data Protection) — por isso vai no **link**, não no corpo do e-mail.
  O token de celular é curto (6 dígitos, TOTP do provider `Phone`) — vai por SMS.
- **`ChangePhoneNumberAsync` em vez de `SetPhoneNumberAsync`** para confirmar
  telefone (ver nota acima).
- **Interfaces próprias** (IEmailSender/ISmsSender) mantêm a Application
  independente do provedor (MailKit/Twilio) — trocável e testável.
- **Fallback de dev ('log')**: permite desenvolver/testar sem custo nem
  credenciais; em produção, basta configurar provider real.
- **`AddDefaultTokenProviders()`**: obrigatório para os token providers de
  (e-mail/telefone) existirem no DI — sem isso, `generation` explode com
  `No IUserTwoFactorTokenProvider named 'Phone' is registered`.

## Comandos executados

- `dotnet add package MailKit` (Infrastructure, versão 4.17.)
- `dotnet build Identity.slnx` → 0 avisos, 0 erros
- `docker compose up -d --build api`
- Chamadas de API (tabela de testes acima)

## Pendências / Próximos passos

- cadastro público do Client (endpoint `register`) — **etapa futura**.
- Configurar credenciais reais de SMTP/Mailtrap e Twilio (trial) em
  user-secrets/env.
- Avaliar policy que exige **celular confirmado** para ações sensíveis
  (hoje apenas informativo no JWT).