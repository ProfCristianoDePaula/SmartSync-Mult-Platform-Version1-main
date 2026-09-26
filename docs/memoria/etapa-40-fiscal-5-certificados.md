# Etapa 40 — Fiscal-5: Certificados digitais

> Mapeamento do roteiro: Fiscal-5 = Etapa 40. R4 com rigor máximo.

## Objetivo

Upload A1 + senha, validação, guarda cifrada, resolução por chamada, rotação, expiração e alertas — sem segredo em git/log/resposta/teste.

## Entregas

- `CertificadoDigital` (tenant + branch/Empty + CNPJ + thumbprint + subject + validades + PFX/senha **cifrados** + KeyId + status + remetente; sem `IsActive`, revoke = status) + `AlertaFiscal` + migration `AddCertificados`.
- `ISecretProtector` AES-256-GCM (envelope nonce12+cipher+tag16, KeyId por linha, pronto p/ KMS) + `FiscalSecretsOptions` (`Fiscal:Secrets`); sem chave mestra **não sobe em Production** (fail-fast), dev/teste usa efêmera com aviso.
- `CertificadoService.UploadAsync`: multipart ≤1 MB, `.pfx/.p12` (A3/nuvem recusados), `LoadPkcs12` sem disco + `EphemeralKeySet`, exige chave privada + vigência, CNPJ ICP-Brasil (subject/SAN) com base comparada aos emitentes do escopo (422 se diverge), rotação (novo revoga anterior), auditoria; senha errada → 400 genérico.
- `ICertificadoResolver` (filial › tenant, só vigentes, re-envelopa na rotação de chave mestra, X509 descartável, nunca cache em claro) + `CertificadoReadModel` real (troca `SemCertificados`; prontidão Fiscal-4 passa a enxergar validade).
- `ICscService`: token CSC write-only cifrado (`csc_token_cifrado`, migration `AddCscToken`), `PUT /emitentes/{id}/csc`, leitura só pelo transmissor.
- `ValidadeCertificadoService` + `CertificadoExpiryWorker` diário (Expirando <30d, Expirado, alerta sem duplicar não-lidos) + `GET/PATCH /api/fiscal/alertas`.
- Controllers com `TenantAdmin/Manager`; DTOs só metadados.

## Decisões (delegadas)

- A3/nuvem indisponíveis (sem implementação específica); cadeia ICP-Brasil registrada sem bloquear (validação online completa = pendência F0-19).
- Ambiente do certificado: resolução ignora ambiente por ora (1 cert por escopo; refinamento futuro).

## Validação

- Build 0 erros (só NU1903); **33/33 testes** (7 novos: PFX gerado no teste, senha errada, vencido/sem chave, base divergente, metadados sem segredo, rotação + isolamento, CSC write-only).
- **Lição registrada (bug real)**: `ConfiguracaoDocumento` nasceu sem `IsActive=true` e sumiu do `HasQueryFilter("Active")` — configs existiam (6/emitente) mas invisíveis; descoberto via contagem com/sem filtro. Correção + teste de regressão (`serie-definida` Ok na prontidão). Checar `IsActive=true` em todo ctor de entidade com query filter virou item de revisão.
