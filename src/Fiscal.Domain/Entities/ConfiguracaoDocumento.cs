using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Configuração por (emitente × tipo × ambiente): habilitado, série, modo de
/// integração (padrão Simulador), referência de certificado e CSC.
/// Homologação e produção em registros separados (R10). O token do CSC e o
/// PFX chegam com o cofre da Fiscal-5 (campo token adicionado lá).
/// </summary>
public sealed class ConfiguracaoDocumento : Entity<ConfiguracaoDocumentoId>
{
    public EmitenteFiscalId EmitenteId { get; private set; }
    public TipoDocumentoFiscal Tipo { get; private set; }
    public AmbienteFiscal Ambiente { get; private set; }
    public bool Habilitado { get; private set; }
    public string Serie { get; private set; } = null!;
    public ModoIntegracao ModoIntegracao { get; private set; }
    public Guid? ReferenciaCertificado { get; private set; }
    public string? CscId { get; private set; }
    public byte[]? CscTokenCifrado { get; private set; }
    public string? CscTokenKeyId { get; private set; }
    public DateTime? HomologacaoValidadaEm { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private ConfiguracaoDocumento() { }

    private ConfiguracaoDocumento(
        ConfiguracaoDocumentoId id, EmitenteFiscalId emitenteId, TipoDocumentoFiscal tipo,
        AmbienteFiscal ambiente, bool habilitado, string serie,
        ModoIntegracao modoIntegracao, Guid? referenciaCertificado, string? cscId)
        : base(id)
    {
        EmitenteId = emitenteId;
        Tipo = tipo;
        Ambiente = ambiente;
        Habilitado = habilitado;
        SetSerie(serie);
        ModoIntegracao = modoIntegracao;
        ReferenciaCertificado = referenciaCertificado;
        CscId = string.IsNullOrWhiteSpace(cscId) ? null : cscId.Trim();
        IsActive = true;
    }

    public static ConfiguracaoDocumento Create(
        EmitenteFiscalId emitenteId, TipoDocumentoFiscal tipo, AmbienteFiscal ambiente,
        bool habilitado, string serie, ModoIntegracao modoIntegracao = ModoIntegracao.Simulador,
        Guid? referenciaCertificado = null, string? cscId = null)
        => new(ConfiguracaoDocumentoId.New(), emitenteId, tipo, ambiente,
            habilitado, serie, modoIntegracao, referenciaCertificado, cscId);

    public void Update(bool habilitado, string serie, ModoIntegracao modoIntegracao,
        Guid? referenciaCertificado, string? cscId)
    {
        Habilitado = habilitado;
        SetSerie(serie);
        ModoIntegracao = modoIntegracao;
        ReferenciaCertificado = referenciaCertificado;
        CscId = string.IsNullOrWhiteSpace(cscId) ? null : cscId.Trim();
    }

    public void Habilitar() => Habilitado = true;

    /// <summary>Marca StatusServico de homologação OK (alimenta a prontidão).</summary>
    public void MarcarHomologacaoValidada() => HomologacaoValidadaEm = DateTime.UtcNow;

    /// <summary>Grava o token CSC cifrado (write-only; nunca devolvido — R4).</summary>
    public void DefinirCscToken(byte[] tokenCifrado, string keyId)
    {
        CscTokenCifrado = tokenCifrado;
        CscTokenKeyId = keyId;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetSerie(string serie)
    {
        if (string.IsNullOrWhiteSpace(serie) || serie.Trim().Length > 3)
            throw new ArgumentException("Série obrigatória (≤3 caracteres).", nameof(serie));
        Serie = serie.Trim();
    }
}
