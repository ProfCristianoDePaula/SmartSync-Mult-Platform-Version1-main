# Etapa 43 — Fiscal-8: Pipeline de emissão, simulador e conciliação

> Mapeamento do roteiro: Fiscal-8 = Etapa 43.

## Objetivo

Fluxo ponta a ponta com SIMULADOR, resiliente a duplicidade e falhas (R6/R7/R11).

## Entregas

- `IAutorizadorFiscal` (Transmitir/Consultar/Cancelar + `DocumentoContexto` com destino fixado) + `SimuladorAutorizador` determinístico (gatilhos `REJEITAR`/`TIMEOUT`/`LENTO` no texto do pedido; protocolo `SIMULACAO-*`; tudo marcado "SIMULAÇÃO — SEM VALOR FISCAL" — R11; sem rede).
- Seleção pelo `ModoIntegracao` do emitente (Simulador padrão; Homologacao/Producao → 400 "integração real indisponível nesta etapa"; produção ainda exige flag+prontidão).
- API: `POST /documentos` (header `Idempotency-Key` obrigatório → **202 + Location** após persistir; repetição idêntica → 200; conteúdo divergente → 409), `GET` lista/detalhe, `POST /{id}/cancelamento` (→202, evento pendente), `POST /{id}/consultar` (conciliação manual), `GET /{id}/xml` (XML didático do simulador).
- Criação: valida → reserva número atômico → monta chave → snapshots → `Validando→Assinado→EnvioPendente` (correção: a máquina Fiscal-7 não permite pular estados) → salva **antes** de qualquer chamada externa.
- `EmissaoWorker` (5 s): reivindica `EnvioPendente` com `FOR UPDATE SKIP LOCKED` (multi-instância), transmite, aplica resultado (200-com-rejeição → Rejeitado, nunca Autorizado), timeout → `ResultadoDesconhecido` + backoff; concilia desconhecidos + `Aguardando` presos + cancelamentos pendentes.
- `IConciliacaoService` (usada pelo worker e pelo endpoint manual): consulta antes de reenviar; cancelamento com timeout fica pendente sem apagar nada.
- Emissão exige concessão `Emitir` (Manager/Seller) ou TenantAdmin; cancelamento igual.
- Migration `AddPipeline` (tentativas/próxima-tentativa/xml).

## Decisões (delegadas)

- Fila = tabela `documentos_fiscais` (intenção durável) + auditoria; outbox transacional dedicado fica para volume que exija (documentado).
- Cancelamento é evento assíncrono (202), não transição imediata.
- XML do simulador é didático (Fiscal-9 gera o leiaute real; Fiscal-11 arquiva).

## Validação

- Build 0 erros; **52/52 testes** (tabela da aula: incompletos 400, duplicadas simultâneas → 1 doc, conflito 409, timeout → desconhecido → consulta autoriza, rejeição nunca autoriza, cancelamento + XML, isolamento).
