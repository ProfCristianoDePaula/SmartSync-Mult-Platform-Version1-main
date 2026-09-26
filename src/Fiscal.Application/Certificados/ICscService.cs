using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Application.Certificados;

/// <summary>
/// Token CSC da NFC-e: gravação write-only cifrada, leitura só pelo
/// transmissor (vida curta, nunca em resposta/log). Id vem do cadastro.
/// </summary>
public interface ICscService
{
    Task DefinirTokenAsync(
        TenantId tenantId, Guid emitenteId, TipoDocumentoFiscal tipo,
        string cscId, string token, Guid userId, CancellationToken ct = default);

    Task<(string CscId, string Token)?> ObterAsync(
        TenantId tenantId, Guid emitenteId, TipoDocumentoFiscal tipo,
        CancellationToken ct = default);
}
