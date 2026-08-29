# Identity & Tenants

Microsserviço de identidade, tenants e filiais da plataforma. Responsável por:

- Cadastro de **Tenants** (empresas clientes) e suas **Filiais (Branches)**;
- **Autenticação e autorização** (JWT) consumidas pelos demais microsserviços da plataforma;
- Login local e **SSO (Google e Facebook/Meta)** para a role `Client`;
- Confirmação de e-mail e validação de celular por SMS.

> Repositório em Clean Architecture + DDD, construído em etapas — o histórico de decisões técnicas fica em [`docs/memoria/INDEX.md`](docs/memoria/INDEX.md).

## Stack

| Camada | Tecnologia |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| Arquitetura | Clean Architecture + DDD (`Identity.Domain`, `Identity.Application`, `Identity.Infrastructure`, `Identity.Api`) |
| Banco de dados | PostgreSQL + EF Core (Npgsql) |
| Identidade | ASP.NET Core Identity + JWT Bearer |
| Documentação/teste de API | [Scalar](https://scalar.com) (`/scalar`) |
| Logging | Serilog |
| Cache distribuído (opcional) | Redis |
| Testes | xUnit + Testcontainers |
| Containers | Docker + Docker Compose |

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) e Docker Compose
- Ferramenta `dotnet-ef` (para migrations): `dotnet tool install --global dotnet-ef`
- Credenciais externas configuradas (Google, Facebook, Twilio, Gmail/MailKit) — siga [`docs/CONFIGURACAO-CREDENCIAIS.md`](docs/CONFIGURACAO-CREDENCIAIS.md) antes de rodar os fluxos de SSO, e-mail e SMS

> Sem Docker também é possível rodar localmente, mas você vai precisar de um PostgreSQL 16+ próprio — veja a seção [Rodando sem Docker](#rodando-sem-docker-opcional).

## 1. Baixando o projeto

```bash
git clone <URL-DO-SEU-REPOSITORIO>
cd <pasta-do-repositorio>
```

## 2. Configurando segredos (ambiente de dev)

Nada de credencial commitada. Em desenvolvimento, use `dotnet user-secrets` na API:

```bash
cd src/Identity.Api
dotnet user-secrets init

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=identity_tenants;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:SigningKey" "<sua-chave-assimetrica-ou-secret>"
dotnet user-secrets set "Authentication:Google:ClientId" "<seu-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<seu-client-secret>"
dotnet user-secrets set "Authentication:Facebook:AppId" "<seu-app-id>"
dotnet user-secrets set "Authentication:Facebook:AppSecret" "<seu-app-secret>"
dotnet user-secrets set "Twilio:AccountSid" "<sid>"
dotnet user-secrets set "Twilio:AuthToken" "<token>"
dotnet user-secrets set "Email:Smtp:User" "<seu-email@gmail.com>"
dotnet user-secrets set "Email:Smtp:Password" "<senha-de-app-ou-oauth>"
```

Para rodar via **Docker Compose**, copie o arquivo de exemplo de variáveis de ambiente e preencha os valores (o `.env` real nunca é commitado):

```bash
cp .env.example .env
```

A lista completa de onde obter cada credencial (Google Cloud Console, Facebook for Developers, Twilio, Gmail) está em [`docs/CONFIGURACAO-CREDENCIAIS.md`](docs/CONFIGURACAO-CREDENCIAIS.md).

## 3. Compilando

```bash
dotnet restore
dotnet build
```

## 4. Rodando em ambiente de testes (Docker Compose)

Sobe API + PostgreSQL (e Redis, se adotado):

```bash
docker compose up -d --build
```

Aplique as migrations (se não estiverem configuradas para rodar automaticamente na subida da API):

```bash
dotnet ef database update \
  --project src/Identity.Infrastructure \
  --startup-project src/Identity.Api
```

Verifique se subiu:

```bash
docker compose ps
curl http://localhost:8080/health
```

Para derrubar o ambiente:

```bash
docker compose down          # mantém o volume do Postgres
docker compose down -v       # remove também os dados do banco
```

## 5. Rodando sem Docker (opcional)

Com um PostgreSQL local rodando e a connection string configurada via `user-secrets`:

```bash
dotnet ef database update --project src/Identity.Infrastructure --startup-project src/Identity.Api
dotnet run --project src/Identity.Api
```

## 6. Testando a API (Scalar)

Com a API no ar, acesse:

```
http://localhost:8080/scalar
```

A UI do Scalar já expõe o esquema OpenAPI e permite colar um Bearer token (obtido em `POST /api/auth/login`) para testar os endpoints protegidos diretamente pelo navegador.

## 7. Rodando os testes automatizados

```bash
dotnet test
```

Os testes de integração usam **Testcontainers** para subir um PostgreSQL real em container — o Docker precisa estar rodando localmente.

## Estrutura do projeto

```
├── src/
│   ├── Identity.Domain/          # Entidades, value objects, regras de negócio
│   ├── Identity.Application/     # Casos de uso, validações, interfaces
│   ├── Identity.Infrastructure/  # EF Core, Identity, integrações (SSO, e-mail, SMS)
│   └── Identity.Api/             # Controllers/endpoints, composição da aplicação
├── tests/
├── docs/
│   ├── memoria/                  # Histórico de decisões técnicas, por etapa
│   └── CONFIGURACAO-CREDENCIAIS.md
├── CONTRATO-IDENTIDADE.md        # Contrato de integração para os demais microsserviços
├── docker-compose.yml
├── .env.example
└── README.md
```

## Documentação adicional

- [`docs/memoria/INDEX.md`](docs/memoria/INDEX.md) — decisões arquiteturais e status de cada etapa do desenvolvimento
- [`docs/CONFIGURACAO-CREDENCIAIS.md`](docs/CONFIGURACAO-CREDENCIAIS.md) — como obter e onde configurar as credenciais externas (Google, Facebook, Twilio, Gmail)
- [`CONTRATO-IDENTIDADE.md`](CONTRATO-IDENTIDADE.md) — formato do JWT, endpoints públicos e como os demais microsserviços devem validar o token (JWKS)
