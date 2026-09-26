using Fiscal.Application.Emissao;
using Fiscal.Application.Repositories;
using Fiscal.Domain.Common;
using Fiscal.Domain.Entities;
using Fiscal.Domain.Enums;
using Fiscal.Infrastructure.Emissao;

namespace Fiscal.Infrastructure.Sefaz;

/// <summary>
/// Seleciona o autorizador pelo ModoIntegracao da configuração do emitente
/// (Simulador padrão; Homologacao/Producao usam o SEFAZ real por ambiente).
/// </summary>
public interface IAutorizadorSelector
{
    Task<IAutorizadorFiscal> ParaAsync(
        TenantId tenantId, EmitenteFiscalId emitenteId,
        TipoDocumentoFiscal tipo, AmbienteFiscal ambiente,
        CancellationToken ct = default);
}

public sealed class AutorizadorSelector(
    IConfiguracaoDocumentoRepository configs,
    SimuladorAutorizador simulador,
    SefazAutorizador sefaz,
    Nfse.NfsePipelineAdapter nfse) : IAutorizadorSelector
{
    public async Task<IAutorizadorFiscal> ParaAsync(
        TenantId tenantId, EmitenteFiscalId emitenteId,
        TipoDocumentoFiscal tipo, AmbienteFiscal ambiente,
        CancellationToken ct = default)
    {
        var cfg = await configs.FindAsync(emitenteId, tipo, ambiente, ct);
        if (cfg?.ModoIntegracao is not (ModoIntegracao.Homologacao or ModoIntegracao.Producao))
            return simulador;
        return tipo == TipoDocumentoFiscal.NFSe ? nfse : sefaz;
    }
}
