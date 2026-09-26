# Como gerar `municipios-ibge.json` completo (Fiscal-3)

> O seed embarcado contém só municípios verificados no ibge.gov.br (São Paulo
> 3550308, Jaú 3525300 — 19/09/2026). A carga total (~5.570) usa a API pública
> de localidades do IBGE **fora do runtime**, uma única vez, e entra pelo
> importador (`POST /api/fiscal/nfse/municipios/importar`) ou regenerando este
> JSON + migration de dados.

## Geração (máquina do operador, com rede)

```bash
curl -s "https://servicodados.ibge.gov.br/api/v1/localidades/municipios?orderBy=nome" \
  | python3 -c "import json,sys; print(json.dumps([{'codigoIbge':str(m['id']),'nome':m['nome'],'uf':m['microrregiao']['mesorregiao']['UF']['sigla']} for m in json.load(sys.stdin)], ensure_ascii=False, indent=1))" \
  > municipios-ibge.json
# conferir contagem (~5.570) e amostra antes de importar
```

## Importação

Converta para CSV (`codigo_ibge;nome;uf;situacao;fonte`) com a situação da
planilha oficial do gov.br e envie com `ignorarMinimo=false`. O relatório
aponta criados/atualizados/ignorados(manual)/rejeitados por linha. Mudança de
fonte nunca apaga dados válidos nem sobrescreve override manual.
