# Etapa 17 — Tenant CPF ou CNPJ (TipoPessoa + Documento)

## Objetivo

**Corrigir uma decisão da Etapa 02**: o `Tenant` foi modelado como CNPJ-only
(empresa). Mas um tenant pode ser **pessoa física** — o documento do tenant
passa a aceitar **CPF (pessoa física) OU CNPJ (pessoa jurídica)**, mantendo a
**unicidade global independente do tipo** (nenhum tenant ativo pode usar o mesmo
número de documento, seja CPF ou CNPJ).

O que muda:

- **Domain**: `Tenant.Cnpj` → `Tenant.Documento` (`Documento` = `TipoPessoa` +
  número), reutilizando os VOs `Cpf`/`Cnpj` para validar conforme o tipo;
- **Banco**: colunas `tipo_pessoa` + `documento` no lugar de `cnpj`; o índice
  único global `IX_tenants_documento` cobre **apenas o número** (independente do
  tipo);
- **Application**: `CreateTenantCommand`/`TenantDto`/`ListTenantsQuery` com
  `tipoPessoa` + `documento`; mensagens de erro diferenciadas (CPF vs CNPJ);
- **API**: payload POST/PUT e filtro `?documento=` de listagem;
- **Migration defensiva** com backfill: tenants existentes (todos CNPJ) viram
  `tipo_pessoa = Juridica`.

## Decisões (registradas)

1. **VO `Documento`** (não dois campos soltos): encapsula `TipoPessoa` + `Numero`
   e delega a validação a `Cpf.Create`/`Cnpj.Create` conforme o tipo — mesmo
   invariante "inválido nunca existe em memória" dos demais VOs (Etapa 02).
2. **Unicidade pelo número, não pelo par (tipo, número)**: `IX_tenants_documento`
   é único sobre a coluna `documento`. Como CPF (11) e CNPJ (14) têm tamanhos
   distintos, uma colisão "cruzada" entre tipos é impossível na prática; mesmo
   assim o índice **não** inclui `tipo_pessoa`, garantindo que o número seja
   único na plataforma independente do tipo.
3. **Documento imutável** no `PUT` (identidade do tenant) — mantém a semântica
   "CNPJ imutável" da Etapa 13, agora para CPF/CNPJ.
4. **Backfill = `Juridica`**: todos os dados pré-existentes nasceram como CNPJ.
   A migration adiciona `tipo_pessoa` nullable, roda
   `UPDATE tenants SET tipo_pessoa = 2`, depois marca NOT NULL (sem default 0
   inválido, que era o que o `dotnet ef` gerava).
5. **`UpdateTenantCommand` não muda**: o documento não é editável, então o PUT
   continua recebendo apenas nomes/e-mail/status (Etapa 13/15).

## Modelagem

### Domain

- `Enums/TipoPessoa.cs` (novo): `Fisica = 1`, `Juridica = 2`.
- `ValueObjects/Documento.cs` (novo): `Tipo` (`TipoPessoa`) + `Numero`.
  `Documento.Create(tipo, value)` delega a `Cpf.Create` (Fisica) ou
  `Cnpj.Create` (Juridica); `Documento.FromValidated(tipo, numero)` para
  hidratar do banco. Igualdade estrutural inclui tipo + número.
- `Entities/Tenant.cs`: `Cnpj` → `Documento` (propriedade, ctor, `Create`,
  comentários). `Update` não recebe documento.

### EF Core

- `TenantConfiguration`:
  - `builder.OwnsOne(x => x.Documento, ...)` → colunas `tipo_pessoa` (int,
    enum) e `documento` (varchar 14);
  - índice único **dentro do OwnsOne**: `documento.HasIndex(d => d.Numero)`
    → `IX_tenants_documento` (único, parcial `WHERE is_active`) — o `HasIndex`
    por string (`"Documento_Numero"`) falhava no design-time ("no property type
    specified"), por isso o índice é configurado via `OwnedNavigationBuilder`;
  - removido o antigo `IX_tenants_cnpj`.

### Application

- `Tenants/CreateTenantCommand`: `Cnpj` → `TipoPessoa TipoPessoa` + `string Documento`.
- `Tenants/TenantDto`: `Cnpj` → `TipoPessoa TipoPessoa` + `string Documento`.
- `Tenants/ListTenantsQuery`: `Cnpj?` → `Documento?`.
- `Tenants/CreateTenantCommandValidator`: `TipoPessoa` `IsInEnum`; `Documento`
  obrigatório; `When(Fisica)` → 11 dígitos ("CPF inválido..."); `When(Juridica)`
  → 14 dígitos ("CNPJ inválido...").
- `Uniqueness/TenantUniquenessValidator`: `EnsureCnpjUniqueAsync` →
  `EnsureDocumentoUniqueAsync(Documento, ...)`; mensagem diferenciada
  ("Já existe um tenant com este CPF/CNPJ."); código `tenant.document.duplicate`.
- `Uniqueness/IUniquenessChecker`: `IsTenantCnpjTakenAsync(Cnpj,...)` →
  `IsTenantDocumentTakenAsync(Documento, ...)`.

### Infrastructure

- `Persistence/UniquenessChecker.cs`: compara `t.Documento.Numero == documento.Numero`
  (OwnsOne não permite comparar o VO inteiro em query).
- `Tenants/TenantService.cs`:
  - `ParseCnpj` → `ParseDocumento(TipoPessoa, raw)` (código `tenant.document.invalid`);
  - `CreateAsync`/`UpdateAsync` usam `Documento`;
  - listagem: filtro `?documento=` normaliza dígitos (11 ou 14) e filtra
    `t.Documento.Numero == digits`;
  - `ToDto` devolve `TipoPessoa` + `Documento.Numero`.
- `PtBrIdentityErrorDescriber`: **sem mudanças** — não tem mensagens de
  documento de tenant (a diferenciação CPF/CNPJ vive nos validators/serviço).

### API

- `TenantsController.List`: query param `cnpj` → `documento` (e doc comment).

### Migration

`20260809230911_TenantDocumentoEmVezDeCnpj` (reescrita defensivamente):

1. `RenameColumn tenants.cnpj → documento` (preserva os dados);
2. `AddColumn tipo_pessoa` **nullable**;
3. **backfill**: `UPDATE tenants SET tipo_pessoa = 2` (todos eram jurídica);
4. `AlterColumn tipo_pessoa` **NOT NULL** (sem default — evita o 0 inválido);
5. `RenameIndex IX_tenants_cnpj → IX_tenants_documento`.

> A versão gerada pelo `dotnet ef` usava `defaultValue: 0` (inválido para o
> enum) e não fazia backfill — corrigida manualmente.

### Testes (115/115 passando, eram 111)

- `TestData`: `UniqueCpf()` novo (base + dígitos verificadores, validado por
  `Cpf.Create`); `CreateTenantAsync` usa `Documento.Create(TipoPessoa.Juridica, UniqueCnpj())`.
- `TenantsTests`:
  - helper `NewTenant(TipoPessoa, documento, email)` (+ overload com Juridica
    implícito para os testes de CNPJ existentes);
  - asserts `tenant.Documento`/`tenant.TipoPessoa` (antes `tenant.Cnpj`);
  - filtro de listagem `?documento=` (antes `?cnpj=`);
  - novos: `CriarTenant_PessoaFisica_Retorna201` (CPF/Fisica),
    `CriarTenant_CpfComTipoJuridica_Retorna400` (CPF rejeitado como CNPJ),
    `CriarTenant_CpfDuplicado_Retorna400`, e o antigo
    `CriarTenant_CnpjObrigatorio_Retorna400` virou
    `CriarTenant_DocumentoObrigatorio_Retorna400`.
- `UniquenessTests`: `EnsureCnpjUniqueAsync`/`tenant.cnpj.duplicate` →
  `EnsureDocumentoUniqueAsync`/`tenant.document.duplicate`; novo
  `Tenant_CpfDuplicado_LancaViolacaoDeRegra`; `Tenant_DocumentoDiferente_PassaValidacao`.

## Comandos executados

- `dotnet build Identity.slnx` → 0 avisos, 0 erros (após ajustes dos testes)
- `dotnet ef migrations add TenantDocumentoEmVezDeCnpj` (reescrita defensiva)
- `dotnet test tests/Identity.Tests/Identity.Tests.csproj` → **115/115 aprovados**
  (migration aplicada no Testcontainers, backfill OK)

## Pendências / Próximos passos

- `GET /api/tenants/me/modules` (contrato recomendado da Etapa 16, §12) segue
  **pendente de implementação**.
- Limites do plano (`MaxBranches`/`MaxUsers`/`MaxStorageMb`) ainda não aplicados
  no cadastro de filiais/usuários.
- Revogação em massa de refresh tokens por tenant no soft delete (Etapa 13).
- Documento do **usuário** (`ApplicationUser.Document`) segue como string livre
  (CPF/CNPJ por role, Etapa 03) — não foi unificado com o `Documento` do tenant
  nesta etapa.
