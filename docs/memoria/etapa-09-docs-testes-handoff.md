# Etapa 09 — Documentação de Contrato, OpenAPI e Testes de Integração

## Objetivo

Tornar o microserviço **"consumível de verdade"** e **verificado**: documentar o
contrato público (OpenAPI nativo 3.1 + `CONTRATO-IDENTIDADE.md`) e criar uma
suíte de testes de integração **com Postgres real** (Testcontainers) que valide
as regras de negócio mais sensíveis (unicidade, acesso por role/policy e fluxos
de confirmação de e-mail/SMS).

## O que foi feito

### 1. OpenAPI nativo (v3.1) com Bearer

- **Substituição**: removido o pacote `Swashbuckle.AspNetCore` completo (SwaggerGen)
  pelo **`Microsoft.AspNetCore.OpenApi` 10.0.10** (geração nativa OpenAPI 3.1) +
  **`Swashbuckle.AspNetCore.SwaggerUI` 10.2.3** apenas para a UI.
- **Transformers** em `src/Identity.Api/OpenApi/`:
  - `BearerSecuritySchemeTransformer` (`IOpenApiDocumentTransformer`): verifica via
    `IAuthenticationSchemeProvider.GetAllSchemesAsync()` se existe o scheme `Bearer`;
    em caso positivo adiciona `components.securitySchemes.Bearer`
    (`type: http`, `scheme: bearer`, `bearerFormat: JWT`).
  - `AuthorizeOperationTransformer` (`IOpenApiOperationTransformer`): por operação,
    só adiciona `security` (referência `OpenApiSecurityRequirement`) quando o endpoint
    tem `[Authorize]` — detectado via
    `context.Description.ActionDescriptor.EndpointMetadata`.
- **Program.cs**: `AddOpenApi` com os dois transformers; `app.MapOpenApi()` agora é
  **sempre exposto** (consumidores precisam da spec mesmo em produção) e a UI
  Swagger fica só em Development apontando para `/openapi/v1.json`.
- **Descobertas da API Microsoft.OpenApi 2.11.0** (importante para futuras edições):
  os tipos estão no namespace **`Microsoft.OpenApi`** (não `...Models.v3`);
  `document.Paths` é `OpenApiPaths` de `IOpenApiPathItem`;
  `PathItem.Operations` é `Dictionary<HttpMethod, IOpenApiOperation>`;
  `SecuritySchemes` é `IDictionary<string, IOpenApiSecurityScheme>` gravável;
  `OpenApiSecurityRequirement` é `IDictionary<OpenApiSecuritySchemeReference, List<string>>`;
  ctor do `OpenApiSecuritySchemeReference`: `(string referenceId, OpenApiDocument
  hostDocument, string externalResource)`.

### 2. Rate limiting configurável

- O rate limit de login/refresh estava **hardcoded**. Passou a ler as seções
  `RateLimiting:Login` / `RateLimiting:Refresh` (record `RateLimiterSection` em
  `src/Identity.Api/RateLimiterSection.cs`) com fallback 5/60 e 10/60.
- Motivo: sem isso os testes de integração compartilham o mesmo IP (`::1`) e
  estourariam o limite; nos testes a configuração é sobrescrita para 1000/3600.

### 3. Contrato publicado

- **`CONTRATO-IDENTIDADE.md`** (raiz): endpoints públicos × protegidos, formato do
  JWT (claims e validação mínima), JWKS, roles, regras de unicidade, fluxos de
  confirmação, rate limits e guia rápido para microsserviços consumidores.

### 4. Suíte de testes de integração (`tests/Identity.Tests`)

- **Stack**: xUnit (template) + `Microsoft.AspNetCore.Mvc.Testing` 10.0.10 +
  `Testcontainers.PostgreSql` 4.13.0 + coverlet (cobertura). Referência ao
  `Identity.Api` e `public partial class Program` (Program.Partial.cs).
- **Fixture compartilhada** `IdentityApiFactory : WebApplicationFactory<Program>,
  IAsyncLifetime` (collection `"integration"`):
  - sobe um **Postgres 16-alpine real** via Testcontainers (db `identity_tests`);
  - `CreateApiClient()` sem auto-redirect;
  - overrides: connection string → container, rate limits 1000/3600, signing key em
    arquivo temporário, OAuth vazio, e **`RemoveAll` + captura** de `IEmailSender`/
    `ISmsSender` (senders capturadores, sem rede).
  - Implementação explícita de `IAsyncLifetime.InitializeAsync/DisposeAsync` para
    resolver o conflito `Task` (interface) × `ValueTask` (base).
- **20 testes passando**, cobrindo:
  - **Unicidade**: CNPJ/e-mail globais de tenant; e-mail/documento únicos por tenant
    (mesmo tenant falha, tenants diferentes ok); constraints reais do banco
    (`DbUpdateException` em `IX_users_tenant_email`).
  - **Acesso/policy**: 401 sem token; 403 com e-mail não confirmado; 401 com senha
    errada; refresh com rotação (antigo revogado); públicos (jwks/health/login) sem
    token; `send-sms-code`/`confirm-phone` exigem token.
  - **Fluxos**: confirmação de e-mail end-to-end (resend → extrai token → confirma →
    login 200); token inválido → 400; SMS (código de 6 dígitos → confirma → celular
    persistido); código inválido → 400.
- **Cobertura** (cobertura XML): **total 72,7% linhas / 24,9% branch**;
  `Identity.Infrastructure` 89,1%/36,1%, `Identity.Application` 82,1%/60,0%,
  `Identity.Domain` 43,5%/29,5%, `Identity.Api` 29,7%/14,1%.

## Testes manuais executados

| # | Cenário | Resultado |
|---|---------|-----------|
| 1 | `dotnet build Identity.slnx` | ✅ 0 errors/0 warnings |
| 2 | `dotnet test` (com Testcontainers) | ✅ 20/20 passando (~5s) |
| 3 | `/swagger/index.html` (dev, container) | ✅ 200 |
| 4 | `/openapi/v1.json` | ✅ openapi 3.1.1, `securitySchemes.Bearer` http/bearer/JWT |
| 5 | Só endpoints com `[Authorize]` têm `security` no OpenAPI | ✅ `send-sms-code`/`confirm-phone`; públicos sem |
| 6 | Rate limiting configurável nos testes | ✅ override 1000/3600 funcional |

## Decisões técnicas e por quê

- **OpenAPI nativo em vez de Swashbuckle Gen**: .NET 10 gera o documento 3.1 sem
  dependência extra; a UI Swagger é só um front-end. Menos pacotes, spec mais atual.
- **`securitySchemes` por operação, não global**: o documento reflete fielmente o
  que é protegido — consumidores não assumem autorização onde não existe.
- **Postgres real (Testcontainers) em vez de SQLite/EF InMemory**: valida os
  **índices únicos** (defesa real do banco) e o SQL do provedor — mocks não
  pegariam violação de constraint.
- **Fixture única por collection**: uma subida do container para todos os testes
  (rápido) com banco limpo por teste via `TRUNCATE`.
- **Senders capturadores**: teste de fluxo de e-mail/SMS sem depender de provedor
  externo — o contrato (token/código no corpo) é validado de ponta a ponta.

## Comandos executados

- `dotnet build Identity.slnx`
- `dotnet test --collect:"XPlat Code Coverage"` (relatório em `coverage/<guid>/`)
- `docker compose build api && docker compose up -d api`
- `curl http://localhost:8080/openapi/v1.json` e verificação visual via `/swagger`

## Pendências / Próximos passos

- **CI/CD**: rodar `dotnet test` (com Testcontainers) + smoke test do container em
  pipeline (GitHub Actions) — herda da Etapa 08.
- **Cobertura do `Identity.Api`** (29,7%): Program.cs/controllers pouco exercitados —
  cenários de SSO e limites 429 são candidatos a novos testes.
- **Rotação de chave JWKS** e **OpenIddict/OIDC** como provedor de identidade
  externo (decisão futura, anotada no INDEX).
