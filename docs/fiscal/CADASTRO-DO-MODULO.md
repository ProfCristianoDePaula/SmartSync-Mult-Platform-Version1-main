# Cadastro do módulo `fiscal` (Fiscal-1 / Etapa 36)

> Nenhum código do Identity foi alterado nesta etapa (ADR-001 não aprovou seed automático). O cadastro usa **exclusivamente** o fluxo oficial existente, com o SuperAdmin (`sa@smartsync.com.br`).

## 1. Criar o módulo

```bash
curl -s -X POST http://localhost:8080/api/modules \
  -H "Authorization: Bearer $SA_TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Fiscal","slug":"fiscal","description":"Emissão de NF-e, NFC-e e NFS-e"}'
# → 201 { id, slug: "fiscal", ... }. Guarde o `id` como MODULE_ID.
```

`slug` é estável e imutável — é ele que o gate `module-fiscal` consulta.

## 2. Criar um plano no módulo

```bash
curl -s -X POST http://localhost:8080/api/modules/$MODULE_ID/plans \
  -H "Authorization: Bearer $SA_TOKEN" -H 'Content-Type: application/json' \
  -d '{"name":"Fiscal Essencial","description":"Emissão fiscal em homologação","monthlyPrice":0,"annualPrice":0,"trialDays":0,"features":["nfe","nfce","nfse"],"maxBranches":null,"maxUsers":null,"maxStorageMb":null}'
# → 201. Guarde o `id` como PLAN_ID.
```

## 3. Vincular o tenant ao módulo

```bash
curl -s -X POST http://localhost:8080/api/tenants/$TENANT_ID/modules \
  -H "Authorization: Bearer $SA_TOKEN" -H 'Content-Type: application/json' \
  -d "{\"moduleId\":\"$MODULE_ID\",\"planId\":\"$PLAN_ID\"}"
# → 201. A partir daqui, tokens do tenant passam no gate (cache de 5 min).
```

## 4. Conferir

```bash
curl -s http://localhost:8080/api/tenants/me/modules \
  -H "Authorization: Bearer $TENANT_TOKEN" | grep fiscal
curl -s http://localhost:8082/api/fiscal/ping \
  -H "Authorization: Bearer $TENANT_TOKEN"
# → 200 {"status":"ok",...} (sem o vínculo: 403; sem token: 401; SuperAdmin sem tenant: 403).
```

## Observações

- Troca de plano / desvinculação seguem `PUT`/`DELETE /api/tenants/{tenantId}/modules/{moduleId}` (histórico preservado para billing).
- Licença do módulo **não** autoriza documentos por si só: cada etapa fiscal tem seu próprio gate (prontidão do emitente + flag de produção a partir da Fiscal-4).
