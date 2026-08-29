# Credenciais de Desenvolvimento — Identity & Tenants

> **Uso exclusivo em desenvolvimento local.** Em produção, credenciais entram
> via variáveis de ambiente (`VARIAVEL__CHAVE`) ou secret manager — **nunca**
> commitadas.
> **Fonte da verdade técnica:** OpenAPI/Scalar em
> [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1).

---

## 1. SuperAdmin de bootstrap

Criado automaticamente na inicialização pelo `DbSeeder` (Etapa 18). O e-mail é
fixo (`sa@smartsync.com.br`); a senha vem do `.env` (default apenas em
Development):

| Variável | Default (só Development) | Nota |
|----------|---------|------|
| `SEED_SUPERADMIN_PASSWORD` | — | **Obrigatória** no `.env` — use senha forte |

> Em Development, se `SEED_SUPERADMIN_PASSWORD` não for definida, o DbSeeder usa
> a senha padrão de desenvolvimento. Fora de Development, o usuário **não** é
> criado sem a variável (veja `docs/SEED-INICIAL.md`).

O SuperAdmin tem `TenantId` nulo → JWT **sem** claim `tenant_id` → opera em
qualquer tenant (módulos, planos, tenants, vínculos, branches, registro de
usuários).

## 2. Obter o JWT (login)

```bash
curl -s -X POST http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"identifier":"sa@smartsync.com.br","password":"SUA_SENHA_DO_ENV"}'
```

Resposta `200` → `accessToken` (15 min) + `refreshToken` (7 dias).

Use nos endpoints protegidos:

```bash
curl -s http://localhost:8080/api/roles \
  -H "Authorization: Bearer $TOKEN"
```

## 3. Cadastro de usuários (`POST /api/auth/register`)

**Acesso:** **Público** (autosserviço). Cria **sempre um `Client`** vinculado
ao `tenantId` (obrigatório), com **e-mail NÃO confirmado** — o login só funciona
após confirmar o link. **Não há campo `type`**; usuários internos
(TenantAdmin/Manager/Seller/Delivery) não são criados via API (pendência).

```bash
curl -s -X POST http://localhost:8080/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{
    "fullName": "Carlos Cliente",
    "email": "carlos@boavista.com.br",
    "password": "Senha!2026x",
    "tenantId": "<id-do-tenant>",
    "document": "98765432100"
  }'
```

> Com o sender de dev (`Email__SmtpHost=''`), o link de confirmação é logado no
> console da API. Depois:
> `curl -s -X POST .../api/auth/confirm-email -H 'Content-Type: application/json'
> -d '{"email":"carlos@boavista.com.br","token":"<token>"}'`.

### 3.3 Client via SSO (Google/Facebook)

Além do registro local, o Client pode entrar por provedor social já sabendo o
tenant (Etapa 05/10):

```bash
# Inicia o handshake (público; passa o tenant do client)
curl -s "http://localhost:8080/api/auth/external-login/google?tenantId=<id-do-tenant>"
```

> Google/Facebook exigem credenciais configuradas — veja
> [CONFIGURACAO-CREDENCIAIS.md](CONFIGURACAO-CREDENCIAIS.md).

## 4. Variáveis de ambiente (dev)

Copie `.env.example` → `.env` (nunca versionado). Variáveis de credenciais
externas (OAuth/SMS/e-mail) entram em dev via `dotnet user-secrets`:

```bash
dotnet user-secrets set "OAuth__Google__ClientId" "..."
dotnet user-secrets set "Sms__Provider" "log"          # "log" em dev / "twilio" p/ real
dotnet user-secrets set "Email__Username" "seu@gmail.com"
```

Veja o mapeamento completo de cada credencial em
[CONFIGURACAO-CREDENCIAIS.md](CONFIGURACAO-CREDENCIAIS.md).

## 5. Segurança — lembrete

- `SEED_SUPERADMIN_PASSWORD` do `.env` é o único "segredo" obrigatório em dev; troque
  antes de qualquer exposição.
- `keys/` (PEM das chaves RSA) e `appsettings.*.local.json` são ignorados pelo
  git — nunca commitá-los.
