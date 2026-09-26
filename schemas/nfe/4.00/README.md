# Pacote oficial de schemas NF-e 4.00 (Fiscal-9)

> Estado em 19/09/2026: **não instalado** — o Portal Nacional (`nfe.fazenda.gov.br`)
> recusou clientes automatizados neste ambiente (loop de 50 redirects em
> `curl`; `webfetch` com erro de transporte). Nenhum XSD foi copiado de
> memória. Ver `docs/fiscal/PENDENCIAS.md` F0-22.

## Como instalar (operador, com navegador)

1. Baixe o Pacote de Liberação + NT 2025.002 vigente em
   `https://www.nfe.fazenda.gov.br/portal/principal.aspx` (Documentos →
   Downloads) e anote versão, data e hash SHA-256.
2. Extraia os `.xsd` nesta pasta (`schemas/nfe/4.00/`).
3. Atualize `schemas/nfe/4.00/MANIFESTO.txt` (modelo abaixo) e rode a suíte
   Fiscal — o validador oficial (`IXsdValidator` modo `oficial/4.00`) passa a
   validar o XML gerado contra o pacote real.

```
# MANIFESTO.txt
origem=https://www.nfe.fazenda.gov.br/portal/...
versao=4.00
nt=NT 2025.002 vX.XX
data_download=AAAA-MM-DD
sha256=<hash do zip>
```

Sem o pacote, o modo oficial responde 400 "pacote oficial ausente"; o modo
`simulador` (XSD didático em `schemas/simulador/`) continua válido para testes.
