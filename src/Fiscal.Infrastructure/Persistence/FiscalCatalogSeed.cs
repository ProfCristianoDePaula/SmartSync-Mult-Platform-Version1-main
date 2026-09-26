using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Fiscal.Infrastructure.Persistence;

/// <summary>
/// Seeds idempotentes do catálogo fiscal (Fiscal-2).
/// UFs: 27 siglas com cUF da tabela do IBGE (conferida em 19/09/2026:
/// ibge.gov.br SP=35 + tabela citada da fonte IBGE para as demais) e
/// autorizadores da Relação de Serviços Web do Portal Nacional da NF-e
/// (SVAN: MA; SVRS: AC,AL,AP,CE,DF,ES,PA,PB,PI,RJ,RN,RO,RR,SC,SE,TO;
/// próprios: AM,BA,GO,MG,MS,MT,PE,PR,RS,SP).
/// Endpoints: SOMENTE URLs confirmadas nas fontes oficiais abaixo, cada uma
/// com fonteUrl + verificadoEm (R3). O que não foi confirmado NÃO entra aqui:
/// ver docs/fiscal/PENDENCIAS.md.
/// </summary>
public static class FiscalCatalogSeed
{
    private static readonly DateTime VerificadoEm = new(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc);

    private const string FontePortalHomolog = "https://hom.nfe.fazenda.gov.br/portal/WebServices.aspx";
    private const string FontePortalProd = "https://www.nfe.fazenda.gov.br/portal/webServices.aspx";
    private const string FonteSefazSp = "https://portal.fazenda.sp.gov.br/servicos/nfe/Paginas/URL-WEBSERVICES.aspx/1000";

    public static async Task SeedAsync(FiscalDbContext db, CancellationToken ct = default)
    {
        if (!await db.UfsFiscais.AnyAsync(ct))
        {
            await db.UfsFiscais.AddRangeAsync(Ufs(), ct);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.SefazEndpoints.AnyAsync(ct))
        {
            await db.SefazEndpoints.AddRangeAsync(Endpoints(), ct);
            await db.SaveChangesAsync(ct);
        }
    }

    private static IEnumerable<UfFiscal> Ufs()
    {
        // (sigla, cUF, nome, autorizadorNFe, autorizadorNFCe).
        // NFC-e segue o mesmo agrupamento da NF-e na página do Portal (sujeito a
        // confirmação na Fiscal-9 — registrado em PENDENCIAS.md F0-12).
        var p = AutorizadorTipo.Proprio;
        var rows = new (string Sigla, int Cuf, string Nome, AutorizadorTipo NFe, AutorizadorTipo NFCe)[]
        {
            ("RO", 11, "Rondônia", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("AC", 12, "Acre", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("AM", 13, "Amazonas", p, p),
            ("RR", 14, "Roraima", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("PA", 15, "Pará", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("AP", 16, "Amapá", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("TO", 17, "Tocantins", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("MA", 21, "Maranhão", AutorizadorTipo.Svan, AutorizadorTipo.Svan),
            ("PI", 22, "Piauí", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("CE", 23, "Ceará", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("RN", 24, "Rio Grande do Norte", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("PB", 25, "Paraíba", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("PE", 26, "Pernambuco", p, p),
            ("AL", 27, "Alagoas", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("SE", 28, "Sergipe", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("BA", 29, "Bahia", p, p),
            ("MG", 31, "Minas Gerais", p, p),
            ("ES", 32, "Espírito Santo", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("RJ", 33, "Rio de Janeiro", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("SP", 35, "São Paulo", p, p),
            ("PR", 41, "Paraná", p, p),
            ("SC", 42, "Santa Catarina", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
            ("RS", 43, "Rio Grande do Sul", p, p),
            ("MS", 50, "Mato Grosso do Sul", p, p),
            ("MT", 51, "Mato Grosso", p, p),
            ("GO", 52, "Goiás", p, p),
            ("DF", 53, "Distrito Federal", AutorizadorTipo.Svrs, AutorizadorTipo.Svrs),
        };

        foreach (var r in rows)
            yield return UfFiscal.Create(r.Sigla, r.Cuf, r.Nome, r.NFe, r.NFCe);
    }

    private static IEnumerable<SefazEndpoint> Endpoints()
    {
        const string V4 = "4.00";
        const string V1 = "1.00";
        var m55 = ModeloFiscal.NFe55;
        var h = AmbienteFiscal.Homologacao;
        var pr = AmbienteFiscal.Producao;

        // AM homolog — Portal Nacional (homologação).
        yield return Ep("AM", m55, SefazServico.StatusServico, h, V4, "https://homnfe.sefaz.am.gov.br/services2/services/NfeStatusServico4", FontePortalHomolog);
        yield return Ep("AM", m55, SefazServico.Autorizacao, h, V4, "https://homnfe.sefaz.am.gov.br/services2/services/NfeAutorizacao4", FontePortalHomolog);
        yield return Ep("AM", m55, SefazServico.ConsultaProtocolo, h, V4, "https://homnfe.sefaz.am.gov.br/services2/services/NfeConsulta4", FontePortalHomolog);
        yield return Ep("AM", m55, SefazServico.RecepcaoEvento, h, V4, "https://homnfe.sefaz.am.gov.br/services2/services/RecepcaoEvento4", FontePortalHomolog);
        yield return Ep("AM", m55, SefazServico.Inutilizacao, h, V4, "https://homnfe.sefaz.am.gov.br/services2/services/NfeInutilizacao4", FontePortalHomolog);
        yield return Ep("AM", m55, SefazServico.ConsultaCadastro, h, V4, "https://homnfe.sefaz.am.gov.br/services2/services/CadConsultaCadastro4", FontePortalHomolog);

        // AM produção — Portal Nacional (produção).
        yield return Ep("AM", m55, SefazServico.StatusServico, pr, V4, "https://nfe.sefaz.am.gov.br/services2/services/NfeStatusServico4", FontePortalProd);
        yield return Ep("AM", m55, SefazServico.Autorizacao, pr, V4, "https://nfe.sefaz.am.gov.br/services2/services/NfeAutorizacao4", FontePortalProd);
        yield return Ep("AM", m55, SefazServico.ConsultaProtocolo, pr, V4, "https://nfe.sefaz.am.gov.br/services2/services/NfeConsulta4", FontePortalProd);
        yield return Ep("AM", m55, SefazServico.RecepcaoEvento, pr, V4, "https://nfe.sefaz.am.gov.br/services2/services/RecepcaoEvento4", FontePortalProd);
        yield return Ep("AM", m55, SefazServico.Inutilizacao, pr, V4, "https://nfe.sefaz.am.gov.br/services2/services/NfeInutilizacao4", FontePortalProd);
        yield return Ep("AM", m55, SefazServico.ConsultaCadastro, pr, V4, "https://nfe.sefaz.am.gov.br/services2/services/CadConsultaCadastro4", FontePortalProd);

        // SP homologação — SEFAZ-SP (URLs dos Web Services).
        yield return Ep("SP", m55, SefazServico.StatusServico, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx", FonteSefazSp);
        yield return Ep("SP", m55, SefazServico.Autorizacao, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx", FonteSefazSp);
        yield return Ep("SP", m55, SefazServico.RetAutorizacao, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferetautorizacao4.asmx", FonteSefazSp);
        yield return Ep("SP", m55, SefazServico.ConsultaProtocolo, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx", FonteSefazSp);
        yield return Ep("SP", m55, SefazServico.RecepcaoEvento, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx", FonteSefazSp);
        yield return Ep("SP", m55, SefazServico.Inutilizacao, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeinutilizacao4.asmx", FonteSefazSp);
        yield return Ep("SP", m55, SefazServico.ConsultaCadastro, h, V4, "https://homologacao.nfe.fazenda.sp.gov.br/ws/cadconsultacadastro4.asmx", FonteSefazSp);

        // AN homologação — seção Ambiente Nacional da página SEFAZ-SP.
        yield return Ep("AN", m55, SefazServico.RecepcaoEvento, h, V4, "https://hom.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx", FonteSefazSp);
        yield return Ep("AN", m55, SefazServico.DistribuicaoDFe, h, V1, "https://hom.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx", FonteSefazSp);

        // AN produção — Portal Nacional (produção).
        yield return Ep("AN", m55, SefazServico.RecepcaoEvento, pr, V4, "https://www.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx", FontePortalProd);
    }

    private static SefazEndpoint Ep(
        string autorizador, ModeloFiscal modelo, SefazServico servico,
        AmbienteFiscal ambiente, string versao, string url, string fonte)
        => SefazEndpoint.Create(autorizador, modelo, servico, ambiente, versao, url,
            fonteUrl: fonte, verificadoEm: VerificadoEm, verificado: true);
}
