# PENDÊNCIAS — Módulo Fiscal (R3: sem fonte oficial = verificado=false)

> Data-base 19/09/2026. Nada aqui é integração concluída.

| ID | Item | Estado |
| --- | --- | --- |
| F0-01 | `Cnpj` alfanumérico (NT Conjunta 2025.001): cronograma e algoritmo — VOs atuais só aceitam 14 dígitos | `verificado=false`. Confirmar texto vigente + adaptar VO na Fiscal-4 |
| F0-02 | NT 2025.002 (IBS/CBS/IS) v1.51: obrigatoriedade desde 03/08/2026, rejeição 1115 como "implementação futura", desobrigação Simples/MEI | `verificado=false`. Confirmar no Portal Nacional antes da Fiscal-9 |
| F0-03 | NT 2025.001 (QR-Code v3 NFC-e): CSC ainda exigido na versão vigente? | `verificado=false` (Fiscal-9) |
| F0-04 | Relação de Web Services NF-e (URLs por UF/autorizador/modelo/serviço/ambiente/versão) | `verificado=false`. Nenhuma URL entra no seed sem `fonteUrl+verificadoEm` (Fiscal-2/7) |
| F0-05 | NFS-e: lista de conveniados (formato/API), convênio por município, `GET /parametros_municipais/...`, instabilidade em homologação | `verificado=false` (Fiscal-3/8/12) |
| F0-06 | `Unimake.DFe`: `LICENSE` no commit fixado, compilação `net10.0`, grupo IBSCBS, DPS nacional, isolamento por chamada | `verificado=false` (condições do ADR-001; Fiscal-1/9/12) |
| F0-07 | `ZeusAutomacao/DFe.NET`: licença + ausência de NFS-e — mantido como não-escolha até prova em contrário | `verificado=false` |
| F0-08 | Schemas XSD (PL + NTs) com origem/data/hash versionados em repo | `verificado=false` (Fiscal-9/12) |
| F0-09 | `tPag`, prazos de cancelamento/inutilização, contingência suportada, retenção legal, DANFE/DANFSE obrigatoriedades | `verificado=false` — viram dados versionados, nunca hardcode (Fiscais 10/11/13/14) |
| F0-10 | UFs/municípios reais além de `Jaú/SP`, `São Paulo/SP`; IBGE de todas as filiais | `verificado=false` (Fiscal-3/4) |
| F0-11 | Preço real por item (hoje `PrecoMock=100`), frete/despesas por leiaute, `tPag` real | `verificado=false` (Fiscal-6/13) |
| F0-12 | Mapeamento NFC-e por UF = mesmo da NF-e (Portal agrupa "demais serviços do sistema NF-e"; confirmar por UF na Fiscal-9) | `verificado=false`. Seed usa mesma hipótese, documentada |
| F0-13 | Endpoints NÃO semeados (sem confirmação em 19/09/2026): SP produção (7 serviços), SVRS/SVAN (todos os serviços/ambientes), demais UFs próprias (BA/GO/MG/MS/MT/PE/PR/RS × 6-7 serviços × 2 ambientes), NFC-e 65, SVC-AN/SVC-RS | `verificado=false`. Entrar via `POST /importar` ou seed futuro só com `fonteUrl` |
| F0-14 | cUF: SP=35 confirmado direto no ibge.gov.br; demais códigos via tabela citada da fonte IBGE (Datacaixa espelho) | Conferir `ibge.gov.br/explica/codigos-dos-municipios.php` antes de produção |
| F0-15 | Hosts NFS-e nacional (sefin/adn producaorestrita + produção) do roteiro §3 | `verificado=false`. Reconfirmar em `gov.br/nfse/.../apis-prod-restrita-e-producao` antes de homologação (Fiscal-12) |
| F0-16 | Carga total de ~5.570 municípios (API localidades IBGE) | Pendente ação do operador: `docs/fiscal/GERAR-MUNICIPIOS.md` + `POST /importar`. Seed atual: São Paulo 3550308 + Jaú 3525300 (verificados) |
| F0-17 | API de convênio/parâmetros por município (módulos CNC/ADN/Parametrização) | `verificado=false`. `INfseParametrizacaoClient` pronto; chamada real só manual |
| F0-18 | A3/nuvem indisponíveis; validação completa da cadeia ICP-Brasil online | `verificado=false`. Upload só `.pfx/.p12`; cadeia registrada sem bloquear |
| F0-19 | `FISCAL_MASTER_KEY` em produção (Key Vault/KMS futuro) + backup/restauração do cofre | Operacional (GO-LIVE): sem a chave o serviço não sobe em Production (proposital) |
| F0-20 | Tabelas oficiais NCM/cClassTrib (formato validado; conteúdo não importado) | `verificado=false`. Importar de fonte oficial ou manter só formato |
| F0-21 | Vetores oficiais de chave de acesso (exemplos do manual) | `verificado=false`. Testes usam autoconistência + tamper; confrontar manual na Fiscal-9 |
| F0-22 | Pacote oficial XSD 4.00 + NT 2025.002 (Portal recusou automação: redirect loop) | `verificado=false`. Instalação manual documentada em `schemas/nfe/4.00/README.md` |
| F0-23 | Algoritmos de assinatura e QR Code NFC-e v3 (NT 2025.001) | `verificado=false`. Defaults 4.00 no código; QR bloqueado até confirmação |
| F0-24 | Envelopes SOAP vs WSDL de cada serviço | `verificado=false`. Confrontar antes de homologação real |
| F0-25 | Contingência (sem modo offline improvisado) | Documentada como pendência; indisponibilidade → Desconhecido/ErroTecnico |
| F0-26 | Tabela cStat completa do manual vigente | Parcial no código (`CStatTabela`); completar antes de homologação |
| F0-27 | Leiaute oficial completo DANFE/DANFCE/DANFSE (PDF simplificado entregue) | Futura biblioteca de PDF com licença verificada ou geração local completa |
| F0-28 | DPS/OpenAPI/schemas NFS-e confrontados com manual vigente | `verificado=false`. Caminhos, nós e tamanhos em `NfseNacionalContrato` |
| F0-29 | Substituição de NFS-e (só se o manual permitir) | Bloqueada com 422; implementar quando confirmado |
| F0-30 | Emissão automática ao finalizar venda + tPag real (Fiscal-13) | Pendente até a integração com o Estoque |
