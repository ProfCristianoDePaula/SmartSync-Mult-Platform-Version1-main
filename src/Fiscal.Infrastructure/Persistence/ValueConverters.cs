using Fiscal.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Fiscal.Infrastructure.Persistence;

/// <summary>
/// Conversores de valor centralizados. Para propriedades NULLABLE, o EF Core
/// envolve automaticamente o conversor (null passa direto) — desde que o
/// conversor seja declarado entre os tipos NÃO anuláveis.
/// </summary>
public static class ValueConverters
{
    public static readonly ValueConverter<TenantId, Guid> Tenant =
        new(v => v.Value, v => TenantId.From(v));

    public static readonly ValueConverter<BranchId, Guid> Branch =
        new(v => v.Value, v => BranchId.From(v));

    public static readonly ValueConverter<AuditLogId, Guid> AuditLog =
        new(v => v.Value, v => AuditLogId.From(v));

    public static readonly ValueConverter<UfFiscalId, Guid> UfFiscal =
        new(v => v.Value, v => UfFiscalId.From(v));

    public static readonly ValueConverter<SefazEndpointId, Guid> SefazEndpoint =
        new(v => v.Value, v => SefazEndpointId.From(v));

    public static readonly ValueConverter<MunicipioId, Guid> Municipio =
        new(v => v.Value, v => MunicipioId.From(v));

    public static readonly ValueConverter<NfseMunicipioConfigId, Guid> NfseMunicipioConfig =
        new(v => v.Value, v => NfseMunicipioConfigId.From(v));

    public static readonly ValueConverter<NfseAmbienteId, Guid> NfseAmbiente =
        new(v => v.Value, v => NfseAmbienteId.From(v));

    public static readonly ValueConverter<NfseImportRunId, Guid> NfseImportRun =
        new(v => v.Value, v => NfseImportRunId.From(v));

    public static readonly ValueConverter<EmitenteFiscalId, Guid> EmitenteFiscal =
        new(v => v.Value, v => EmitenteFiscalId.From(v));

    public static readonly ValueConverter<ConfiguracaoDocumentoId, Guid> ConfiguracaoDocumento =
        new(v => v.Value, v => ConfiguracaoDocumentoId.From(v));

    public static readonly ValueConverter<ConcessaoUnidadeId, Guid> ConcessaoUnidade =
        new(v => v.Value, v => ConcessaoUnidadeId.From(v));

    public static readonly ValueConverter<CertificadoDigitalId, Guid> CertificadoDigital =
        new(v => v.Value, v => CertificadoDigitalId.From(v));

    public static readonly ValueConverter<AlertaFiscalId, Guid> AlertaFiscal =
        new(v => v.Value, v => AlertaFiscalId.From(v));

    public static readonly ValueConverter<ProdutoFiscalId, Guid> ProdutoFiscal =
        new(v => v.Value, v => ProdutoFiscalId.From(v));

    public static readonly ValueConverter<ClienteFiscalId, Guid> ClienteFiscal =
        new(v => v.Value, v => ClienteFiscalId.From(v));

    public static readonly ValueConverter<NaturezaOperacaoId, Guid> NaturezaOperacao =
        new(v => v.Value, v => NaturezaOperacaoId.From(v));

    public static readonly ValueConverter<DocumentoFiscalId, Guid> DocumentoFiscal =
        new(v => v.Value, v => DocumentoFiscalId.From(v));

    public static readonly ValueConverter<EventoFiscalId, Guid> EventoFiscal =
        new(v => v.Value, v => EventoFiscalId.From(v));

    public static readonly ValueConverter<SerieNumeracaoId, Guid> SerieNumeracao =
        new(v => v.Value, v => SerieNumeracaoId.From(v));
}
