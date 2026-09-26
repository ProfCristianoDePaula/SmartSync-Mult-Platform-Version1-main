# PERGUNTAS — Módulo Fiscal (Fiscal-0 / Etapa 35)

> Só o que depende do usuário e pode mudar o desenho (R14). Sem essas respostas a Fiscal-1 pode começar (esqueleto), mas Fiscal-4/6/10/12/14 ficam condicionadas.

## Bloqueantes de desenho

1. **Regimes tributários dos tenants-alvo** — Quais CRTs a v1 deve suportar (1 Simples, 2 Simples excesso, 3 Normal, 4 MEI)? Há MEI emitindo NFS-e nacional? (Afeta validação formal, IBSCBS por CRT/data e bloqueio de CPF da P3.)
2. **UFs e municípios iniciais** — Além de `Jaú/SP` e `São Paulo/SP`, quais UFs/filiais emitem primeiro? Alguma filial tem CNPJ próprio hoje (fora do sistema) ou todas usam o CNPJ do tenant? (Afeta `EmitenteFiscal`, sequência por emitente e prioridade dos adapters.)
3. **NFC-e sim ou não na v1?** — Roteiro inclui NFC-e 65 (com CSC). Mantemos NFC-e junto da NF-e desde a Fiscal-9/10 ou adiamos para focar NF-e 55 + NFS-e?
4. **Quem configura e quem emite?** — Confirmar: TenantAdmin configura qualquer filial do tenant; Manager/Seller só com concessão por filial (a criar)? Seller pode emitir ou só Manager? Delivery/Client nunca? (Afeta modelo de concessão por `Branch`.)
5. **Retenção de XML/eventos** — Prazo operacional desejado (sem definir prazo legal por conta própria — R3)? Disco/volume por tenant/ano/mês/chave atende, ou exige S3-compatível desde a v1?
6. **Certificados** — A1 em cofre com `FISCAL_MASTER_KEY` atende? Algum emitente usa A3/nuvem na v1 (então fica indisponível até implementação específica)?
7. **Integração Venda→Fiscal** — Emissão automática ao finalizar venda (flag por emitente, padrão false) ou sempre manual via `POST /api/vendas/{id}/emitir-nota`? Venda mista gera NF-e + NFS-e separadas (ok)?
8. **NFS-e municipal** — Se algum município-alvo estiver em `SomenteAdn`/`ProvedorMunicipal`, priorizamos qual conector após o nacional, ou registramos como indisponível com mensagem orientativa?

## Não-bloqueantes (responder quando possível)

- `tPag` por `FormaPagto` (Pix/Transferência/Depósito/Débito/Crédito) — mapeamento desejado antes da Fiscal-13?
- DANFE/DANFCE/DANFSE: geração local ou serviço oficial (módulo DANFSe)? Biblioteca de PDF com licença aceita?
- Alertas/métricas: destino (Prometheus + quais alertas mínimos)?
