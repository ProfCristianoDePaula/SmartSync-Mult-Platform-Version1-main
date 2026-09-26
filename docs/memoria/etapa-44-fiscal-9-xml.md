# Etapa 44 — Fiscal-9: NF-e/NFC-e XML, XSD e assinatura (offline)

> Mapeamento do roteiro: Fiscal-9 = Etapa 44. Nada transmitido.

## Objetivo

Gerar XML válido no schema vigente, assinado e verificável — sem rede.

## Entregas

- `INFeXmlBuilder` (subconjunto fiel 4.00: ide/emit/dest/det/imposto/total/transp/pag/infAdic + IBSCBS quando a regra exige; produto sem NCM recusado; 1–990 itens) + `IbsCbsRegras` versionada (NT 2025.002 v1.51, vigência 03/08/2026, só CRT 3 — reconfirmar, F0-02).
- `IXsdValidator` (R8: DTD proibido, resolver nulo, 5 MB; modo `simulador` com XSD didático + localização da raiz; modo `oficial/4.00` exige pacote, 400 claro se ausente).
- `IXmlSigner` (enveloped+C14N, KeyInfo X509, RSA-SHA256/SHA256 do 4.00 — F0-23; **Signature irmã do infNFe**, como na NF-e real; verificação + tamper).
- `schemas/nfe/4.00/README.md` (instalação do pacote oficial) + `schemas/simulador/nfe-simplificada.xsd` (didático, IBSCBS opcional).
- NFC-e QR Code: **bloqueado com mensagem clara** até confirmação da NT 2025.001 (sem QR inventado — R3).

## Decisões (delegadas)

- Portal recusou automação (redirect loop) → pacote oficial é pendência operacional (F0-22), não bloqueio de código.
- Correções reais: ordem dos args `WriteElementString`, IBSCBS no XSD, posição da Signature, `Unique` de idempotência com catch de corrida.

## Validação

- Build 0 erros; **58/58 testes** (golden XSD + assinatura, tamper, XXE, sem-NCM, pacote ausente, IBSCBS×CRT); container healthy.
