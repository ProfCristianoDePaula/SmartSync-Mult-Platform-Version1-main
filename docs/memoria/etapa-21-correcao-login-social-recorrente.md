# Etapa 21 — Correção do Login Social Recorrente (Google/Facebook)

## Objetivo

Corrigir o login social para que um **Client que já se cadastrou por um provedor
(Google/Facebook) consiga fazer login de novo (login recorrente)**. O sintoma
relatado: qualquer e-mail já existente parecia ser tratado como tentativa de
takeover e bloqueado com **401**, mesmo quando era o **mesmo usuário** voltando a
entrar pelo **mesmo provedor** — contrariando o que a Etapa 05 já definia
("fluxo de login social recorrente associa a um Client já existente").

Esta etapa também **resolve de vez o bloqueio da suíte de testes** do ambiente
(Smart App Control) — a suíte completa passa a rodar num container Linux.

## Diagnóstico (causa raiz CONFIRMADA — Etapa 21)

A hipótese inicial do usuário ("o código decide só pela existência do e-mail e
nunca checa `UserManager.FindByLoginAsync`") **NÃO se confirmou literalmente**:
o `SocialAuthService.LoginAsync` **já chamava** `FindByLoginAsync(provider,
providerKey)` primeiro e só caía no e-mail se não houvesse vínculo.

As causas raiz **reais** (confirmadas no código e reproduzidas em teste):

1. **Criação do primeiro login NÃO atômica (causa raiz do 401 recorrente):**
   `CreateAsync` → `AddToRoleAsync` → `AddLoginAsync` eram **três escritas sem
   transação**. Se qualquer passo falhasse **depois** do `CreateAsync`, o método
   retornava `null` (401), mas o usuário **já existia no banco SEM o vínculo em
   `AspNetUserLogins`**. A partir daí, o login recorrente (que depende do
   vínculo) nunca encontrava a conta e caía no branch "e-mail já existe" → 401
   para sempre. Conta "trancada".
2. **401 genérico indistinguível:** o controller colapsava **todas** as falhas
   (conflito real anti-takeover, tenant inexistente, sem role Client) num único
   `401 "Não foi possível completar o login social"` — impossível de depurar e
   de o frontend tratar corretamente.

O conflito anti-takeover legítimo (e-mail existe na plataforma mas **nunca** foi
vinculado àquele provedor) estava funcionando como a Etapa 05 projetou; o
problema é que ele era **indistinguível** dos demais erros.

## Correção aplicada

### 1. Resultado tipado em vez de `TokenResponse?`

Novo `SocialAuthResult` (`src/Identity.Application/Auth/SocialAuthResult.cs`)
com `SocialAuthError` tipado. O `ISocialAuthService.LoginAsync` agora retorna
`Task<SocialAuthResult>` (tokens ou erro) em vez de `TokenResponse?` (onde todo
fracasso era `null`).

### 2. Ordem de verificação corrigida (`SocialAuthService.LoginAsync`)

1. **(a) Login recorrente:** `UserManager.FindByLoginAsync(provider,
   providerKey)` (usando o `ExternalLoginInfo`/`providerKey` do callback). Se
   achar a conta → valida role/tenant e emite tokens **sem re-checar e-mail**.
2. **(b) Sem vínculo:** cria um novo Client **ou** retorna `EmailConflict` se o
   e-mail já existe na plataforma sem vínculo a esse provedor (anti-takeover,
   Etapa 05).
3. **(c) Primeiro login:** criação **ATÔMICA** — usuário + role Client +
   vínculo `AddLoginAsync(user, new UserLoginInfo(provider, providerKey,
   provider))` na **mesma transação** (`BeginTransactionAsync`/`CommitAsync`).
   Esse é o passo que garantia que a próxima tentativa de login recorrente
   encontraria o vínculo. Sem a transação, uma falha parcial deixava usuário
   órfão.

### 3. Erros diferenciados no controller (`ExternalAuthController`)

| Cenário | Erro tipado | HTTP | Mensagem |
|---------|-------------|------|----------|
| TenantId ausente (query/state) | `MissingTenantId` | **400** | "O parâmetro 'tenantId' é obrigatório no login social." |
| Tenant inexistente | `TenantNotFound` | **400** | "Tenant não encontrado." |
| Provider não suportado | `ProviderUnsupported` | **400** | "Provedor social não suportado." |
| Falha ao persistir (criação atômica) | `CreateFailed` | **400** | "Não foi possível criar a conta vinculada ao provedor." |
| E-mail existe sem vínculo ao provedor | `EmailConflict` | **401** | "Já existe uma conta com este e-mail cadastrada de outra forma." |
| Conta vinculada a outro tenant | `TenantMismatch` | **401** | "A conta vinculada ao provedor pertence a outro tenant." |
| Conta sem a role Client | `NotClient` | **403** | "A conta vinculada ao provedor não possui a role Client." |

Regra: **erro de request → 400**, **conflito de credencial → 401**, **autenticou
mas não autoriza → 403**.

### 4. Bug adicional descoberto na validação end-to-end (scheme do Challenge)

Ao configurar as credenciais reais do Google e subir a API nativa, o endpoint de
início retornava **500**: o controller passava o valor da rota (ex.: `google`,
minúsculo) ao `Challenge(properties, provider)`, mas o scheme registrado pelo
`AddGoogle`/`AddFacebook` tem o nome canônico **`Google`/`Facebook`** → "No
authentication handler is registered for the scheme 'google'". Esse bug ficou
**latente desde a Etapa 05** (só era alcançável com credenciais configuradas —
antes o endpoint respondia 400 "não suportado"). Corrigido normalizando o scheme
para o nome canônico antes do `Challenge` e gravando o nome canônico no `state`.

## Resolução do bloqueio da suíte (Smart App Control)

- **Causa confirmada:** Smart App Control do Windows (política WDAC,
  `VerifiedAndReputablePolicyState=1`) bloqueia o carregamento de assemblies
  recém-buildados no `bin` de testes com `0x800711C7` (reproduzido: o .NET
  lança `FileLoadException` ao carregar `Identity.Infrastructure.dll`).
- **Não é desativável via registro/Defender** (é política da Microsoft; exige
  ação manual no Windows Security e não pode ser religada sem reinstalar o SO).
- **Solução implementada:** `scripts/run-tests-in-docker.ps1` — roda o
  `dotnet test` dentro de um **container Linux** (sem a política), montando o
  projeto e o socket do Docker para o Testcontainers subir o Postgres real.
  Ajustes de networking: `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` +
  `--add-host host.docker.internal:host-gateway`; `TESTCONTAINERS_RYUK_DISABLED`
  para contornar o Resource Reaper em Docker Desktop.

## Testes

Novo `tests/Identity.Tests/SocialAuthTests.cs` (7 testes, chamando
`ISocialAuthService` diretamente — os schemes OAuth não são registrados em
testes por não haver credenciais reais; Postgres real via Testcontainers):

| Cenário | Teste | Resultado esperado |
|---------|-------|--------------------|
| Primeiro login cria o Client e grava o vínculo | `PrimeiroLoginSocial_CriaClientEGravaVinculoDoProvedor` | Sucesso + `FindByLoginAsync` encontra a conta + 1 usuário no e-mail |
| Login recorrente reutiliza sem recriar | `SegundoLoginSocial_MesmoProvedor_ReutilizaContaSemCriarNada` | 2º login sucesso, **1 única** conta no e-mail |
| E-mail existe sem vínculo → conflito | `LoginSocial_EmailExistenteSemVinculo_RetornaEmailConflict` | `EmailConflict` |
| Vinculado sem role Client | `LoginSocial_UsuarioVinculadoSemRoleClient_RetornaNotClient` | `NotClient` |
| Tenant inexistente | `LoginSocial_TenantInexistente_RetornaTenantNotFound` | `TenantNotFound` |
| Conta de outro tenant | `LoginSocial_ContaVinculadaAOutroTenant_RetornaTenantMismatch` | `TenantMismatch` |
| Provider não suportado | `LoginSocial_ProvedorNaoSuportado_RetornaProviderUnsupported` | `ProviderUnsupported` |

### Correção colateral (latente da Etapa 20)

Ao rodar a **suíte completa** pela primeira vez no container, `ModulesTests.
CriarModulo_SuperAdmin_Retorna201` falhava com 400: a Etapa 20 adicionou em
`TenantModulesTests.ConsultarMeusModulos_Retorna200ComSlugENomeDoPlano` um
módulo **"SmartSync Agro"** (criação direta no banco) e o teste de módulos cria
outro com o **mesmo nome** via API — nome de módulo é único entre ativos.
Corrigido dando nome único ao módulo do `TenantModulesTests` (era colisão de
ordem de execução que a suíte completa nunca tinha exposto por causa do
bloqueio do ambiente).

## Comandos executados

- `.\scripts\run-tests-in-docker.ps1 -Filter "FullyQualifiedName~SocialAuthTests"` → **7/7**
- `.\scripts\run-tests-in-docker.ps1` (suíte completa) → **135/135**
- `dotnet user-secrets set "OAuth:Google:ClientId/ClientSecret" ... --project src/Identity.Api`
- API nativa (Development, `http://localhost:8080`, Postgres do compose):
  - `GET /api/auth/external-login/google?tenantId=<id>` → **302** para
    `accounts.google.com` com `client_id` correto e
    `redirect_uri=http://localhost:8080/signin-google`;
  - sem `tenantId` → **400**; provider não suportado → **400**.

## Validação end-to-end (Google real) — CONCLUÍDA

Credenciais do Google criadas no console (app OAuth 519661235668). Redirect URI
registrado no console: `http://localhost:8080/signin-google`. A validação dos 3
cenários foi feita no navegador com a API rodando em `http://localhost:8080`
(Postgres do compose). Resultado:

| Cenário | Log da API | HTTP | Banco |
|---------|------------|------|-------|
| 1º login (`dev.cristianodepaula@gmail.com`, e-mail novo) | `Login social concluído ... resultado=success` | **200** | usuário criado no tenant + vínculo `Google \| sub` gravado em `AspNetUserLogins` + role Client |
| Login recorrente (mesma conta Google) | `Login social concluído ... resultado=success` | **200** | **1 único** usuário e **1 único** vínculo (nada recriado — `FindByLoginAsync` reutiliza) |
| E-mail existente sem vínculo (`prof.cristianodepaula@gmail.com`) | `Login social recusado ... resultado=EmailConflict` | **401** | conta local intacta, **nenhum** vínculo novo (anti-takeover) |

Também foram validados: `start` → **302** para o Google com `client_id` correto e
`redirect_uri=http://localhost:8080/signin-google`; sem `tenantId` → **400**;
provider não suportado → **400**. O controller ganhou log estruturado do
resultado do login social (sucesso/erro tipado) para observabilidade em
produção.

### Nota: como rodar a API com SSO neste ambiente

- **Escalada do Smart App Control:** após o build no container Linux (que grava
  os bins no volume do host), o SAC passou a **bloquear também os bins nativos**
  (`0x800711C7` em `Identity.Infrastructure.dll`), mesmo após `dotnet clean` +
  rebuild 100% nativo e até copiando o bin para outro caminho (o bloqueio é
  **por conteúdo**, não por caminho). Ou seja: **a API nativa não sobe mais**
  neste Windows.
- **Caminho robusto = container** (como a suíte de testes): `docker compose up -d
  --build api`. Credenciais OAuth agora vão por **env** (o container não enxerga
  `dotnet user-secrets`):
  - `docker-compose.yml` mapeia `OAuth__Google__ClientId/ClientSecret` e
    `OAuth__Facebook__ClientId/ClientSecret` (vazio = recurso desabilitado);
  - valores atuais do Google gravados em `.env` (não commitar);
  - a porta 8080 mapeada para o host preserva o redirect URI `localhost:8080/signin-google`.
- Estado atual: container `identity_api` rodando, health **OK** e `start` do
  Google validado (**302** com `client_id` correto e PKCE).

## Pendências / Próximos passos

- **Facebook** continua sem credenciais criadas/configuradas (console Meta for
  Developers) — mesmo fluxo da Etapa 05, com o fix da Etapa 21 já aplicado.
- **API nativa bloqueada pelo SAC** (por conteúdo) — para testes manuais de SSO,
  usar o container `identity_api` (com `OAuth__*` no `.env`). Estado atual:
  container rodando e saudável.
