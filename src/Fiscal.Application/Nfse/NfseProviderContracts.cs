namespace Fiscal.Application.Nfse;

public sealed record DpsRequest(
    Fiscal.Domain.Common.TenantId TenantId,
    Guid EmitenteId,
    Fiscal.Domain.Enums.AmbienteFiscal Ambiente,
    Guid DocumentoId,
    string CodigoIbgeMunicipio,
    string SerieDps,
    int NumeroDps,
    string DpsXml,
    string? CodigoServico);

public sealed record ResultadoNfse(
    bool Sucesso,
    string? ChaveAcesso,
    string? NumeroNfse,
    string? CodigoVerificacao,
    string? Motivo,
    string? Protocolo);

public sealed record CancelarNfseRequest(
    Guid DocumentoId,
    string ChaveAcesso,
    string Motivo);

public sealed record ResultadoEventoNfse(
    bool Sucesso,
    string? Motivo,
    string? Protocolo);

/// <summary>
/// Provedor NFS-e (Etapa 47: nacional; municipais futuros sem implementar
/// centenas de webservices). Resolução por município + contribuinte + vigência.
/// </summary>
public interface INfseProvider
{
    string Nome { get; }
    bool Atende(string codigoIbgeMunicipio);
    Task<ResultadoNfse> EmitirAsync(DpsRequest request, CancellationToken ct);
    Task<ResultadoNfse> ConsultarAsync(string identificador, CancellationToken ct);
    Task<ResultadoEventoNfse> CancelarAsync(CancelarNfseRequest request, CancellationToken ct);
}
