# Spike — Bibliotecas fiscais .NET (Fiscal-0 / Etapa 35)

> Descartável, FORA das soluções (`Identity.slnx`, `Estoque.slnx`, futura `Fiscal.slnx`). Nenhum projeto foi adicionado a solução alguma; nenhum pacote foi instalado no `src/` ou `tests/`.
> Nenhuma chamada a SEFAZ, gov.br, prefeituras ou webservices fiscais foi feita. Fontes consultadas em 19/09/2026: páginas NuGet (`Unimake.DFe`, `Zeus.Net.NFe.NFCe`, perfil `ZeusAutomacao`) e GitHub (`Unimake/DFe`, `ZeusAutomacao/DFe.NET`, fork `Hercules-NET/ZeusFiscal`). `verificado=false` onde falta confirmação em código/Portal Nacional.

## Candidatos e versões consultadas

| Candidato | Versão/estado consultado | Cobertura anunciada |
| --- | --- | --- |
| Unimake.DFe | `20260918.1641.36` (18/09/2026, 1 dia antes da etapa); 442,6 mil downloads totais | NFe, NFCe, CTe, CTeOS, CTeSimp, MDFe, GNRe, NFSe, eSocial, EFD-Reinf, DARE-SP, NF3e, NFCom, DCe, NFGas, NFe-ABI (descrição NuGet) |
| ZeusAutomacao/DFe.NET (+ pacotes `Zeus.Net.NFe.NFCe` `2026.8.18.2047`) | Pacote atualizado há ~8 dias; 750 mil downloads (NFe/NFCe); repo `ZeusAutomacao/DFe.NET` | Geração de NFe (2.0/3.10/4.0) e NFCe (3.10/4.0) + consumo dos serviços (descrição do repo). Sem menção a NFS-e/DPS |
| Implementação própria (sem SDK) | N/A — baseline de comparação | Só o que o time implementar + schemas oficiais versionados em repo |

## Matriz dos 6 critérios do roteiro

| Critério | Unimake.DFe | ZeusAutomacao/DFe.NET | Própria |
| --- | --- | --- | --- |
| (a) Compila e roda em .NET 10 | **Compatível por target, compilação real pendente.** Targets declarados `netstandard2.0` + `net472`; NuGet calcula compatível com `net10.0` (página do pacote lista `net10.0 computed`). `verificado=false`: compilar spike `net10.0` com referência + teste de serialização mínima na Fiscal-1 antes de fixar versão. | **Compatível por target, compilação real pendente.** Pacote `Zeus.Net.NFe.NFCe` declara `net6.0` + `netstandard2.0` + `net462` (compatível com net10 por forward-compat). Fork `Hercules-NET/ZeusFiscal` cita `.NET 6.0+/8.0`. `verificado=false` idem. | Sim, por construção (mesmo SDK do repo). Custo é tempo, não compatibilidade. |
| (b) NF-e 4.00 + NT 2025.002 vigente | **Indício positivo, não verificado.** Releases quase semanais em set/2026 (ex.: notas citam `nAdicao` opcional, QR Code NFe-ABI, fixes NFe/NFSe) sugerem acompanhamento de NTs, mas **não confirmei** grupo IBSCBS / v1.51 / Ato Conjunto nº 1/2026 nesta spike. `verificado=false` → conferir no Portal Nacional + teste de serialização do grupo IBSCBS na Fiscal-9. | **Parcial, a confirmar (premissa do roteiro diz "parcial").** Repo cobre NFe 4.00 como leiaute, mas adequação à Reforma (IBSCBS) não confirmada aqui. `verificado=false`. | Depende do time acompanhar PL + NTs e versionar schemas (`schemas/nfe/<versao>/`). Viável, porém mais lento. |
| (c) NFC-e | Sim (anunciado: NFCe + classes `NFCeListagemChaves` na doc API). QR Code/CSC a confirmar por versão de NT na Fiscal-9. `verificado=false` p/ QR v3 + CSC. | Sim (NFe/NFCe é o escopo central; DANFE NFCe nativo citado). QR/CSC por versão a confirmar. `verificado=false`. | Idem (b): implementar conforme NT vigente. |
| (d) NFS-e Padrão Nacional (DPS) | Sim (anunciado: NFSe + classes `Servicos.NFSe`, ex.: `ConsultarNfsePDF(XmlDocument, Configuracao)` na doc API). DPS/GZip/mTLS por ambiente **a confirmar em código** na Fiscal-12. `verificado=false`. | **Não encontrado.** Escopo anunciado é NFe/NFCe (+CTe/MDFe em pacotes irmãos). Nada sobre DPS/NFS-e nacional. Trata-se como **não atende** até prova em contrário. | Implementar DPS conforme manual/OpenAPI vigentes (Etapa 12). |
| (e) Certificado e endpoints POR CHAMADA, sem estado global | **Indício positivo, a confirmar.** API de serviços recebe `Configuracao` por instância (ex.: `ConsultarNfsePDF(XmlDocument, Configuracao)`), padrão compatível com multi-tenant (um `Configuracao`/certificado por emitente/ambiente por chamada). `verificado=false`: teste com duas configurações simultâneas (CNPJ-A homolog + CNPJ-B homolog) na Fiscal-1/9, mais isolamento de handlers (R8/R9 do material-base: sem compartilhar `HttpClient` com certificado entre emitentes). | **A confirmar.** Arquitetura clássica com objetos de configuração por emissão, mas não inspecionei código nesta spike. `verificado=false`. Exigir o mesmo teste de isolamento antes de adotar. | Total por construção (resolver certificado/endpoint por tentativa a partir do cadastro próprio). |
| (f) Licença comercial | **MIT (indício forte).** Página GitHub `Unimake/DFe` exibe selo/ba seção `MIT license`; README descreve biblioteca open source multiplataforma. `verificado=false`: ler `LICENSE` do commit fixado antes de adicionar referência. | **A verificar — risco.** Fork espelho cita `LGPL-2.1`; perfil original não confirma licença nesta spike. `verificado=false`: ler `LICENSE` de `ZeusAutomacao/DFe.NET` no commit fixado; LGPL em serviço SaaS exige análise antes de adotar. | Sem risco de licença (código próprio). |

## Recomendação da spike (a ratificar no ADR-001)

1. **Adotar `Unimake.DFe` como candidata principal** (única que anuncia NFe + NFCe + NFSe nacional, MIT indicado, releases ativas em 19/09/2026, `netstandard2.0` compatível com net10), **condicionada** a: (i) leitura do `LICENSE` no commit fixado; (ii) spike de compilação `net10.0` na Fiscal-1; (iii) teste de isolamento por chamada; (iv) confirmação do grupo IBSCBS/NT 2025.002 no Portal Nacional antes da Fiscal-9.
2. **Descartar ZeusAutomacao/DFe.NET como base do Fiscal** (sem NFS-e nacional anunciada + licença não confirmada), salvo se a Fiscal-1 provar o contrário — registrar então em novo ADR.
3. **Manter "implementação própria" como plano B** por documento/versão (ex.: se um grupo da NT não estiver suportado, implementar só esse trecho com schemas oficiais versionados, sem trocar a base).

## O que NÃO foi feito (de propósito)

- Sem `dotnet add package`, sem build, sem certificado, sem chamada a SEFAZ/gov.br/prefeitura, sem segredos. Tudo acima é metadado público + documentação; cada `verificado=false` virou pendência em `docs/fiscal/PENDENCIAS.md`.
