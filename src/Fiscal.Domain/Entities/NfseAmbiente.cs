using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Endpoints NFS-e por município (ou "*" = padrão do modo) e ambiente.
/// Produção e homologação em registros separados, sem replicação por
/// substituição de host (R10). `Verificado=false` até confirmação.
/// </summary>
public sealed class NfseAmbiente : Entity<NfseAmbienteId>
{
    public string CodigoIbge { get; private set; } = null!;
    public ModoEmissaoNfse Modo { get; private set; }
    public AmbienteFiscal Ambiente { get; private set; }
    public string? BaseUrlSefin { get; private set; }
    public string? BaseUrlAdn { get; private set; }
    public string? BaseUrlParametros { get; private set; }
    public bool Verificado { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private NfseAmbiente() { }

    private NfseAmbiente(
        NfseAmbienteId id, string codigoIbge, ModoEmissaoNfse modo,
        AmbienteFiscal ambiente, string? baseUrlSefin, string? baseUrlAdn,
        string? baseUrlParametros, bool verificado)
        : base(id)
    {
        SetCodigoIbge(codigoIbge);
        Modo = modo;
        Ambiente = ambiente;
        BaseUrlSefin = NormUrl(baseUrlSefin);
        BaseUrlAdn = NormUrl(baseUrlAdn);
        BaseUrlParametros = NormUrl(baseUrlParametros);
        Verificado = verificado;
        IsActive = true;
    }

    public static NfseAmbiente Create(
        string codigoIbge, ModoEmissaoNfse modo, AmbienteFiscal ambiente,
        string? baseUrlSefin, string? baseUrlAdn, string? baseUrlParametros,
        bool verificado = false)
        => new(NfseAmbienteId.New(), codigoIbge, modo, ambiente,
            baseUrlSefin, baseUrlAdn, baseUrlParametros, verificado);

    public void Update(string? baseUrlSefin, string? baseUrlAdn, string? baseUrlParametros, bool verificado)
    {
        BaseUrlSefin = NormUrl(baseUrlSefin);
        BaseUrlAdn = NormUrl(baseUrlAdn);
        BaseUrlParametros = NormUrl(baseUrlParametros);
        Verificado = verificado;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetCodigoIbge(string codigoIbge)
    {
        var v = (codigoIbge ?? "").Trim();
        if (v != "*" && new string(v.Where(char.IsDigit).ToArray()).Length != 7)
            throw new ArgumentException("Código IBGE deve conter 7 dígitos ou '*' (padrão do modo).", nameof(codigoIbge));
        CodigoIbge = v == "*" ? "*" : new string(v.Where(char.IsDigit).ToArray());
    }

    private static string? NormUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException($"URL deve ser HTTPS absoluta: {url.Trim()}.", nameof(url));
        return url.Trim();
    }
}
