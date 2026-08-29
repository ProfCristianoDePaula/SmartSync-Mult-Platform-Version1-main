# Execução para Testes — Identity & Tenants API

> **Status:** Atualizado (Etapa 18). Guia de **inicialização** da API para testar
> todos os endpoints de ponta a ponta, tanto em **Development local** quanto via
> **Docker Compose** (full stack).

Pré-requisitos: [.NET SDK](https://dotnet.microsoft.com/download) e [Docker](https://www.docker.com/).

---

## 1. Opção A — Development local (API via `dotnet run`)

### 1.1 Postgres isolado (ou use o do compose)

```pwsh
docker run -d --name iddev-pg `
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=identity `
  -p 55432:5432 postgres:16-alpine
```

> O cache pode ser `memory` em dev (dispensa o Valkey). Querendo testar o cache
> Redis, suba também o Valkey: `docker run -d --name iddev-valkey -p 6379:6379 valkey/valkey:8-alpine`.

### 1.2 Subir a API em Development

Na raiz do repositório:

```pwsh
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://localhost:8080'
$env:ConnectionStrings__DefaultConnection='Host=localhost;Port=55432;Database=identity;Username=postgres;Password=postgres'
$env:Cache__Mode='memory'
$env:SEED_SUPERADMIN_PASSWORD='SuaSenhaForte!2026'
# IMPORTANTE: forçar o sender de e-mail de dev (console). Se SmtpHost vier
# preenchido (ex.: user-secrets), o register/forgot tentam SMTP real e o
# register pode falhar com 500.
$env:Email__SmtpHost=''
dotnet run --no-launch-profile --project src/Identity.Api
```

Na inicialização, em **Development**:

- as **migrations** são aplicadas automaticamente;
- o `IdentitySeeder` registra as **roles** (`SuperAdmin`, `TenantAdmin`,
  `Manager`, `Seller`, `Delivery`, `Client`);
- o `DbSeeder` cria, se ainda não existir: Module `SmartSync Core` → Plan
  `Full Access` → Tenant `SmartSync Platform` → Branch → vínculo → **SuperAdmin
  de bootstrap `sa@smartsync.com.br`** (senha = `SEED_SUPERADMIN_PASSWORD`).

> **Atenção:** `Jwt__SigningKeyPath` é opcional — a API **auto-gera** o PEM em
> `keys/jwt-signing-key.pem` se o arquivo não existir. Se reutilizar a mesma
> base Postgres entre execuções, mantenha o mesmo PEM (senão os tokens antigos
> ficam inválidos).
>
> **Fora de Development** (`Production` etc.) o SuperAdmin **não é criado sem**
> `SEED_SUPERADMIN_PASSWORD` — o seed pula a criação do usuário.

### 1.3 Conferir que subiu

- Swagger UI: <http://localhost:8080/swagger> (não usar em produção)
- Scalar: <http://localhost:8080/scalar/v1>
- Health: <http://localhost:8080/api/health>
- OpenAPI: <http://localhost:8080/openapi/v1.json>

---

## 2. Opção B — Docker Compose (full stack)

`postgres` + `valkey` + `api` (imagem da `Dockerfile`).

```pwsh
# 1. Definir a senha do SuperAdmin (OBRIGATÓRIA — o compose falha sem ela)
#    copie .env.example -> .env e ajuste SEED_SUPERADMIN_PASSWORD (e POSTGRES_*, API_PORT...)

# 2. Subir a stack
docker compose up -d --build

# 3. Validar
docker compose ps
curl -s http://localhost:8080/api/health
```

- A API sobe em **Production** por padrão (`ASPNETCORE_ENVIRONMENT`); para usar
  Scalar/Swagger local, defina `ASPNETCORE_ENVIRONMENT=Development` no `.env`.
- `CACHE_MODE=redis` é o default do compose (usa o serviço `valkey`).
- A chave JWT fica em um volume persistente (`jwtkeys`).

---

## 3. Teste de ponta a ponta (todos os endpoints)

Ambiente de teste: `$base = "http://localhost:8080"`. Troque `$pw` pela senha do
SuperAdmin definida em `SEED_SUPERADMIN_PASSWORD`.

```pwsh
# Variáveis
$base = "http://localhost:8080"
$pw   = "SuaSenhaForte!2026"   # = SEED_SUPERADMIN_PASSWORD

# 1. Health (público) + JWKS (público)
curl -s $base/api/health
curl -s $base/api/auth/jwks

# 2. Login SuperAdmin (rate limit 5/min)
$login = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' `
  -Body '{"identifier":"sa@smartsync.com.br","password":"'"$pw"'"}'
$h = @{ Authorization = "Bearer $($login.accessToken)" }

# 3. Roles (autenticado)
curl -s -H @h $base/api/roles

# 4. Modules (SuperAdmin)
$m = Invoke-RestMethod -Uri "$base/api/modules" -Method Post -Headers $h -ContentType 'application/json' `
  -Body '{"name":"Agro Teste","slug":"agro-teste","description":"t"}'
$mid = $m.id
curl -s -H @h "$base/api/modules?page=1&pageSize=10"
curl -s -H @h "$base/api/modules/$mid"
curl -s -X PUT -H @h -ContentType 'application/json' -Uri "$base/api/modules/$mid" -Body '{"name":"Agro Teste 2","description":"x"}'

# 5. Plans (SuperAdmin, aninhado no módulo)
$p = Invoke-RestMethod -Uri "$base/api/modules/$mid/plans" -Method Post -Headers $h -ContentType 'application/json' `
  -Body '{"name":"Básico","monthlyPrice":0,"annualPrice":0,"trialDays":0,"features":["x"],"maxBranches":null,"maxUsers":null,"maxStorageMb":null}'
$pid = $p.id
curl -s -H @h "$base/api/modules/$mid/plans?includeInactive=false"
curl -s -H @h "$base/api/modules/$mid/plans/$pid"

# 6. Tenants (SuperAdmin) — tipoPessoa é INT (1=Fisica, 2=Juridica)
$t = Invoke-RestMethod -Uri "$base/api/tenants" -Method Post -Headers $h -ContentType 'application/json' `
  -Body '{"legalName":"Boa Vista LTDA","tradeName":"Boa Vista","tipoPessoa":2,"documento":"11222333000181","email":"contato@boavista.com.br"}'
$tid = $t.id
curl -s -H @h "$base/api/tenants?page=1&pageSize=10&status=Active"
curl -s -H @h "$base/api/tenants/$tid"
# PUT exige legalName/tradeName/email/status (sem tipoPessoa/documento):
curl -s -X PUT -H @h -ContentType 'application/json' -Uri "$base/api/tenants/$tid" `
  -Body '{"legalName":"Boa Vista LTDA","tradeName":"Boa Vista Agro","email":"contato@boavista.com.br","status":1}'

# 7. Vínculo tenant↔módulo (SuperAdmin)
Invoke-RestMethod -Uri "$base/api/tenants/$tid/modules" -Method Post -Headers $h -ContentType 'application/json' `
  -Body '{"moduleId":"'"$mid"'","planId":"'"$pid"'"}'
curl -s -H @h "$base/api/tenants/$tid/modules"                          # só ativos
curl -s -H @h "$base/api/tenants/$tid/modules?includeInactive=true"     # histórico
curl -s -X PUT -H @h -ContentType 'application/json' -Uri "$base/api/tenants/$tid/modules/$mid" `
  -Body '{"planId":"'"$pid"'"}'

# 8. Branches (SuperAdmin ou TenantAdmin do próprio tenant)
$b = Invoke-RestMethod -Uri "$base/api/tenants/$tid/branches" -Method Post -Headers $h -ContentType 'application/json' `
  -Body '{"name":"Filial Centro","address":{"street":"Rua A","number":"1","district":"Centro","city":"SP","state":"SP","postalCode":"01310100"},"contact":{"phone":"11999991234"}}'
curl -s -H @h "$base/api/tenants/$tid/branches"
curl -s -H @h "$base/api/tenants/$tid/branches/$($b.id)"

# 9. Register Client (PÚBLICO, sem token) + confirm-email (PÚBLICO)
curl -s -X POST -ContentType 'application/json' -Uri "$base/api/auth/register" `
  -Body '{"fullName":"Carlos","email":"carlos@boavista.com.br","password":"Senha!2026x","tenantId":"'"$tid"'","document":"98765432100"}'
# confirm-email: com o sender de dev, o link (com o token) é logado no console
# da API como "[E-MAIL (dev, não enviado)] ... confirm-email?email=...&token=..." .
# Extraia o token (URL-encoded), decodifique e confirme:
#   $token = [System.Web.HttpUtility]::UrlDecode('<token>')
curl -s -X POST -ContentType 'application/json' -Uri "$base/api/auth/confirm-email" -Body '{"email":"carlos@boavista.com.br","token":"<token>"}'

# 10. Login Client (após confirmar e-mail) + refresh-token + logout/logout-all
$cl = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -ContentType 'application/json' `
  -Body '{"identifier":"carlos@boavista.com.br","password":"Senha!2026x"}'
curl -s -X POST -ContentType 'application/json' -Uri "$base/api/auth/refresh-token" -Body '{"refreshToken":"'"$($cl.refreshToken)"'"}'
curl -s -X POST -Headers @{ Authorization = "Bearer $($cl.accessToken)" } -ContentType 'application/json' `
  -Uri "$base/api/auth/logout" -Body '{"refreshToken":"'"$($cl.refreshToken)"'"}'
curl -s -X POST -Headers @{ Authorization = "Bearer $($cl.accessToken)" } "$base/api/auth/logout-all"

# 11. Negativos esperados
curl -s -o NUL -w "%{http_code}" $base/api/modules                                        # 401 (sem token)
curl -s -o NUL -w "%{http_code}" $base/api/tenants/$tid/branches/$($b.id)                 # 404 (branch errada)
curl -s -o NUL -w "%{http_code}" -X POST -ContentType 'application/json' -Uri "$base/api/auth/login" `
  -Body '{"identifier":"sa@smartsync.com.br","password":"errada"}'                       # 401
```

### Observações importantes

- **`tenantId` no register é obrigatório**; sem confirmar o e-mail o login do
  Client retorna **`401`** ("credenciais inválidas ou conta não confirmada").
- Se o register **persistir o usuário mas falhar no envio do e-mail** (ex.:
  SMTP sem `Email__SmtpHost=''`), não re-registre o mesmo documento/e-mail (400);
  use `POST /api/auth/resend-confirmation-email` para gerar novo link.
- Branches de outro tenant pelo TenantAdmin retornam `403` (claim `tenant_id` ≠
  `{tenantId}` da rota).
- `POST /api/auth/register` é **público** e cria **sempre um `Client`** — usuários
  internos (TenantAdmin/Manager/Seller/Delivery) não têm endpoint ainda (pendência).
- Unicidade de documento/e-mail tratada na aplicação responde `400` (não `409`).
- **Rate limits:** `login` 5/min, `refresh`/`register`/recuperação 10/min
  (configuráveis em `appsettings.json`).

---

## 4. Troubleshooting

| Sintoma | Causa provável / solução |
|---------|--------------------------|
| `401` no login do Client mesmo com senha certa | E-mail **não confirmado** — confirme antes (`POST /api/auth/confirm-email`). |
| `500` no `register`/`forgot-password` (erro SMTP/SSL) | `Email:SmtpHost` veio preenchido (ex.: user-secrets) — rode com `$env:Email__SmtpHost=''` para usar o sender de dev (console). |
| `400` "Já existe um usuário deste tenant com este documento" | Register anterior persistiu o usuário — use `resend-confirmation-email` em vez de re-registrar o mesmo documento/e-mail. |
| Login do SuperAdmin falha | `SEED_SUPERADMIN_PASSWORD` não é a senha do `sa@smartsync.com.br` na base (mudou o env depois do seed) — recrie a base ou ajuste. |
| `docker compose up` falha no `api` | Falta `SEED_SUPERADMIN_PASSWORD` no `.env` (o compose aborta de propósito). |
| Tokens antigos param de validar | A chave JWT mudou (novo volume/`Jwt__SigningKeyPath`) — mantenha o mesmo PEM. |
| `401/403` em endpoints autenticados | Token ausente/vencido, role insuficiente, ou `{tenantId}` da rota ≠ claim `tenant_id`. |
| `429 Too Many Requests` | Excedeu o rate limit — aguarde o intervalo (config em `appsettings.json`). |
