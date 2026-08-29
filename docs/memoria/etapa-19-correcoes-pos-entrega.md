# Etapa 19 — Correções Pós-Entrega: E-mail Transacional, Celular por Código Numérico e Contrato de Módulos

## Objetivo

Etapa de **manutenção/correções** feita depois da Etapa 18 (seed), validando em
execução real e corrigindo pontos que só apareceram com o ambiente configurado
(SMTP do Gmail, Twilio trial, token de confirmação por clique e contrato de
vínculo tenant↔módulo). Agrupa:

1. **E-mail transacional** configurado e validado com SMTP real (Gmail/Senha de
   App) + correção do **link de confirmação** para apontar à própria API;
2. **Persistência das chaves do DataProtection** — tokens de confirmação/reset
   deixaram de quebrar a cada restart/rebuild do container;
3. **Validação de celular** migrada do token nativo do Identity para **código
   numérico de 6 dígitos** (hash no banco), no mesmo padrão do reset por SMS;
4. **Diagnóstico da limitação do trial da Twilio** (o "482913" fixo é o template
   `sms_2fa`, não o código gerado);
5. **Contrato `TenantModuleDto` sem `planName`** — plano referenciado apenas por
   `planId` (FK).

Nenhuma mudança de escopo de negócio: são correções de entrega, configuração e
contrato de API.

## Decisões (registradas)

1. **E-mail real via Gmail SMTP (Senha de App)**. O `SmtpEmailSender` passa a
   logar o envio bem-sucedido (host/porta/destinatário) para rastreio. Em envio
   real, o Gmail **aceitou** a mensagem (round-trip ~2,6s, sem erro) — entrega
   ao inbox depende do filtro de spam/segurança do Gmail, fora do nosso
   controle.
2. **Link de confirmação aponta para a API**, não mais para um front
   (`localhost:3000`) que não existe. Como o clique no e-mail é um **GET**, foi
   criado `GET /api/auth/confirm-email` que devolve **página HTML** simples
   (sucesso/erro); o `POST /confirm-email` original permanece para clientes.
3. **DataProtection persistido em arquivo** (`DataProtection:KeyPath`, default
   `keys/dataprotection`, volume em Docker). Sem isso, a recriação do container
   regenerava as chaves e **invalidava todos os tokens** (confirmação de e-mail,
   reset de senha) emitidos antes do restart. `SetApplicationName("Identity")`
   garante estabilidade do anel de chaves.
4. **Correção de ownership do volume `jwtkeys`** (`/app/keys` era `root:root`):
   o Dockerfile já faz `chown` (linha 35), mas o volume **pré-existente** mantinha
   o dono antigo, impedindo o app (UID 1654) de gravar as chaves — corrigido com
   `chown -R app:app /app/keys` no volume existente.
5. **Validação de celular por código numérico curto próprio** (6 dígitos), no
   padrão do reset por SMS: apenas o **hash SHA-256** é persistido, validade de
   **10 minutos**, invalidação de códigos anteriores não usados, e o código fica
   **vinculado ao número** informado. Substitui o token nativo do Identity
   (`GenerateChangePhoneNumberTokenAsync`), que é uma string longa — inviável de
   digitar e invisível no SMS do trial.
6. **Trial Twilio = template fixo (mantido)**: o `Body` é o **nome** do template
   (`sms_2fa`) e o conteúdo é genérico da Twilio. O `482913` observado é esse
   conteúdo fixo — **não** é o código gerado pela API (confirmado: hashes
   diferentes no banco, gerador aleatório via `RandomNumberGenerator`). Decisão
   do usuário: **manter o modo trial**; o código real só aparece no SMS após o
   upgrade da conta (`SMS_BODY_TEMPLATE` vazio no `.env`).
7. **`TenantModuleDto` sem `planName`**: o plano é referenciado apenas por
   `planId` (FK). O nome é obtido na tabela de planos quando necessário (o vínculo
   `tenant_modules` nunca armazenou o nome — apenas `plan_id`).
8. **`SEED_SUPERADMIN_PASSWORD` alinhado à senha real** do SuperAdmin
   (`Sm@rtSync2026!`) — o valor no `.env` não batia com o que o usuário usa.

## Modelagem

### Infrastructure

- `Notifications/SmtpEmailSender.cs`: injetado `ILogger<SmtpEmailSender>`; log
  de sucesso `E-mail enviado com sucesso via {Host}:{Port} para {To}` após o
  `SendAsync`.
- `Auth/AuthService.cs`:
  - `ResendConfirmationEmailAsync`: loga o motivo quando **nada** é enviado
    ("e-mail não cadastrado" / "conta já confirmada") — resposta continua neutra;
  - `SendSmsCodeAsync`: `GenerateNumericCode(6)`; invalida códigos anteriores
    não usados (`UsedAtUtc`); persiste `PhoneVerificationCode` (hash + phone +
    `ExpiresAtUtc` = +10 min); envia SMS com o código;
  - `ConfirmPhoneAsync`: valida `code.Length == 6 && all digits`, busca o
    registro por (user, phone, hash) não usado, checa expiração, marca `UsedAtUtc`
    e grava `PhoneNumber` + `PhoneNumberConfirmed = true` via `UpdateAsync`.
- `Persistence/Identity/PhoneVerificationCode.cs` (novo): `Id`, `UserId`,
  `PhoneNumber`, `CodeHash` (SHA-256), `CreatedAtUtc`, `ExpiresAtUtc`,
  `UsedAtUtc?`; `PhoneVerificationCodeConstants.Lifetime = 10 min`.
- `Persistence/Configurations/PhoneVerificationCodeConfiguration.cs` (novo):
  tabela `phone_verification_codes` (colunas `user_id`, `phone_number`,
  `code_hash`, datas), índice `IX_phone_verification_codes_user_id`.
- `Persistence/IdentityDbContext.cs`: + `DbSet<PhoneVerificationCode> PhoneVerificationCodes`.
- `DependencyInjection.cs`: `AddDataProtection().SetApplicationName("Identity")
  .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(DataProtection:KeyPath)))`.
- `TenantModules/TenantModuleService.cs`: removida a resolução do **nome do
  plano** (`ToDtoAsync`/`ToDtoListAsync`/`MapDto` deixam de consultar `Plans`;
  só o módulo continua sendo resolvido).

### Application

- `TenantModules/TenantModuleDto.cs`: removido o campo `PlanName` (record passa a
  ter `..., ModuleId, ModuleName, PlanId, Status, ...`).

### API

- `Endpoints/AuthController.cs`: + `GET confirm-email` (`[FromQuery] email, token`)
  que chama `ConfirmEmailAsync` e devolve `Content(html, "text/html")` com página
  de sucesso ou erro (sempre `200`, amigável ao navegador).

### Migration

`20260810011555_AddPhoneVerificationCodes`: cria `phone_verification_codes`
(Id uuid PK, `user_id`, `phone_number` varchar(20), `code_hash` varchar(64),
`created_at_utc` default `now()`, `expires_at_utc`, `used_at_utc?`; índice em
`user_id`).

### Config / ambiente

- `src/Identity.Api/appsettings.json`: `ConfirmationUrlTemplate` →
  `http://localhost:8080/api/auth/confirm-email?email={email}&token={token}`;
  nova seção `DataProtection:KeyPath: keys/dataprotection`.
- `docker-compose.yml`: + `DataProtection__KeyPath: /app/keys/dataprotection`
  (no volume `jwtkeys`).
- `.env`: `SEED_SUPERADMIN_PASSWORD=Sm@rtSync2026!` (alinhado à conta real);
  arquivo reparado após uma atualização acidental ter concatenado tudo numa linha.
- Documentação: `docs/CONTRATO-IDENTIDADE.md` (§10 lista de campos do
  `TenantModuleDto` e §12 exemplo sem `planName`), `docs/FLUXO-DE-CADASTRO.md`
  (resposta `201` do vínculo sem `planName`), `docs/memoria/etapa-15-modulos-e-planos-por-modulo.md`
  (descrição do DTO sem `PlanName`).

## Testes (118/118 passando)

- `tests/Identity.Tests/TenantModulesTests.cs`: removidas as 2 asserções de
  `PlanName` (`link.PlanName` e `upgraded.PlanName`) — o vínculo continua
  validado por `PlanId`. Nenhuma contagem quebrou.
- **Sem teste automatizado novo** para `send-sms-code`/`confirm-phone` (validado
  em execução real, ver abaixo) — registrado como pendência.

## Verificação operacional (execução real, container Docker)

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `resend-confirmation-email` com SMTP real | `200` em ~2,6s; log `E-mail enviado com sucesso via smtp.gmail.com:587`; e-mail chega ao inbox (spam/segurança do Gmail pode filtrar) |
| 2 | `resend` para conta **já confirmada** | `200` neutro + log `conta já confirmada` (nada é enviado) |
| 3 | `GET /confirm-email` com token inválido | página HTML "Link inválido" (`200`) |
| 4 | `GET /confirm-email` com token real | página "E-mail confirmado"; `EmailConfirmed = t` no banco |
| 5 | Chaves do DataProtection sobrevivem ao rebuild | arquivo `key-*.xml` presente antes e depois de `docker compose up -d api` |
| 6 | `send-sms-code` | `200` (Twilio respondeu `201`); código de 6 dígitos persistido (hash) |
| 7 | `confirm-phone` com o código certo | `200` "Celular validado"; `PhoneNumberConfirmed = t` |
| 8 | `confirm-phone` reutilizando o código | `400` "Código inválido ou expirado" |
| 9 | Login `sa@smartsync.com.br` | `200` com a senha real (após alinhar o `.env`) |
| 10 | `GET /api/tenants/{tenantId}/modules` | resposta **sem** `planName` (só `planId`) |

Observações operacionais:

- A conta `cristiano.depaula@live.com` estava **confirmada por confirmação manual
  anterior** (token extraído do log de dev, quando o SMTP ainda não existia —
  por isso o usuário nunca recebeu o e-mail). Foi **desconfirmada** no banco e o
  e-mail real reenviado; o fluxo completo (resend → link → `GET confirm-email`)
  foi revalidado.
- Usuários de teste criados durante a validação (`teste.tel@teste.com`, e-mails
  descartáveis) foram **removidos** do banco ao final.
- Logs de debug temporários (link de confirmação e código de verificação) foram
  adicionados apenas para capturar o token/código em execução e **removidos**
  depois — tokens/códigos não ficam em log.

## Comandos executados

- `dotnet ef migrations add AddPhoneVerificationCodes --project src/Identity.Infrastructure --startup-project src/Identity.Api --output-dir Persistence/Migrations`
- `dotnet build Identity.slnx` → 0 avisos, 0 erros
- `dotnet test tests/Identity.Tests/Identity.Tests.csproj` → **118/118 aprovados**
- `docker compose build api && docker compose up -d api` (várias iterações)
- `docker exec -u root identity_api chown -R app:app /app/keys` (correção de
  ownership do volume `jwtkeys`)
- Chamadas de API de verificação (tabela acima) via `curl` com Bearer do
  SuperAdmin

## Pendências / Próximos passos

- `GET /api/tenants/me/modules` (contrato recomendado da Etapa 16, §12) segue
  **pendente**.
- **Adicionar teste de integração** para `send-sms-code`/`confirm-phone` (fluxo
  hoje validado manualmente em execução real).
- Limites do plano (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) ainda não aplicados
  no cadastro de filiais/usuários.
- Revogação em massa de refresh tokens por tenant no soft delete (Etapa 13).
- Documento do **usuário** (`ApplicationUser.Document`) segue como string livre.
- **Após o upgrade da conta Twilio**: esvaziar `SMS_BODY_TEMPLATE` no `.env`
  (recriar o container) para o código real de 6 dígitos ir no SMS (e-mail/reset
  e celular).
- **Alternativa registrada** (não implementada): **Twilio Verify** como produto
  próprio de OTP — envia código aleatório real também no trial (exige um
  `ServiceSid` de Verify no console + número verificado).
