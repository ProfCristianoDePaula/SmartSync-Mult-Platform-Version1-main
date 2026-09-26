# Etapa 41 — Fiscal-6: Cadastro fiscal de produtos, clientes e serviços

> Mapeamento do roteiro: Fiscal-6 = Etapa 41. Sem motor tributário (P4).

## Objetivo

Completar dados fiscais reaproveitando cadastros (produto do Estoque, cliente do Identity), com validação formal e prévia legível.

## Entregas

- `ProdutoFiscal` (tenant+produto únicos; NCM/CEST/origem/unidades/GTIN/CFOPs/CST-CSOSN/PIS-COFINS/IPI/IBS-CBS + serviço LC116/NBS/trib-nacional/ISS; tudo informado) + `ClienteFiscal` (tenant+cliente únicos; documento 11/14, indicador IE, endereço IBGE, consumidor final) + `NaturezaOperacao` (código único por tenant) + migration `AddCadastrosFiscais`.
- `IValidadorFiscal`: pronto NF-e/NFC-e/NFS-e por (perfil + CRT + documento) com pendências legíveis (`GET /produtos/pendencias?produtoId=&crt=`; enumeração vem do Estoque na Fiscal-13).
- Regra CRT: Simples/MEI exigem CSOSN; Normal exige CST (só formato/presença).
- Importação CSV de produtos com erro por linha; CRUD naturezas; validação de cliente (IE p/ contribuinte).
- Controllers TenantAdmin/Manager (escrita) + leitura tenant.

## Decisões (delegadas)

- Perfis no Fiscal por ID (sem FK cross-DB); NCM/CEST como formato (tabelas oficiais grandes fora — PENDENCIAS F0-20).
- Alíquotas informadas, nunca calculadas; devolução/ST/importação seguem pendentes (matriz na Fiscal-10/14).

## Validação

- Build 0 erros; **39/39 testes** (6 novos: pronto/pendente, NCM 400, CSV, cliente IE, natureza + isolamento); tabelas criadas no container.
