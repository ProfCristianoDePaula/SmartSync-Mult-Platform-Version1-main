using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Endpoint SEFAZ do catálogo global (SuperAdmin): autorizador × modelo ×
/// serviço × ambiente × versão. URLs vêm SOMENTE daqui (R8) — nunca do
/// cliente HTTP. Sem fonte oficial, o registro nasce com
/// <c>Verificado=false</c> e vai para <c>docs/fiscal/PENDENCIAS.md</c> (R3).
/// </summary>
public sealed class SefazEndpoint : Entity<SefazEndpointId>
{
    public string Autorizador { get; private set; } = null!;
    public ModeloFiscal Modelo { get; private set; }
    public SefazServico Servico { get; private set; }
    public AmbienteFiscal Ambiente { get; private set; }
    public string VersaoServico { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public DateTime? VigenciaInicio { get; private set; }
    public DateTime? VigenciaFim { get; private set; }
    public string? FonteUrl { get; private set; }
    public DateTime? VerificadoEm { get; private set; }
    public bool Verificado { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private SefazEndpoint() { }

    private SefazEndpoint(
        SefazEndpointId id,
        string autorizador,
        ModeloFiscal modelo,
        SefazServico servico,
        AmbienteFiscal ambiente,
        string versaoServico,
        string url,
        DateTime? vigenciaInicio,
        DateTime? vigenciaFim,
        string? fonteUrl,
        DateTime? verificadoEm,
        bool verificado)
        : base(id)
    {
        SetAutorizador(autorizador);
        Modelo = modelo;
        Servico = servico;
        Ambiente = ambiente;
        SetVersaoServico(versaoServico);
        SetUrl(url);
        VigenciaInicio = vigenciaInicio;
        VigenciaFim = vigenciaFim;
        FonteUrl = string.IsNullOrWhiteSpace(fonteUrl) ? null : fonteUrl.Trim();
        VerificadoEm = verificadoEm;
        Verificado = verificado;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static SefazEndpoint Create(
        string autorizador,
        ModeloFiscal modelo,
        SefazServico servico,
        AmbienteFiscal ambiente,
        string versaoServico,
        string url,
        DateTime? vigenciaInicio = null,
        DateTime? vigenciaFim = null,
        string? fonteUrl = null,
        DateTime? verificadoEm = null,
        bool verificado = false)
        => new(SefazEndpointId.New(), autorizador, modelo, servico, ambiente,
            versaoServico, url, vigenciaInicio, vigenciaFim, fonteUrl, verificadoEm, verificado);

    public void Update(
        string versaoServico,
        string url,
        DateTime? vigenciaInicio,
        DateTime? vigenciaFim,
        string? fonteUrl,
        DateTime? verificadoEm,
        bool verificado)
    {
        SetVersaoServico(versaoServico);
        SetUrl(url);
        VigenciaInicio = vigenciaInicio;
        VigenciaFim = vigenciaFim;
        FonteUrl = string.IsNullOrWhiteSpace(fonteUrl) ? null : fonteUrl.Trim();
        VerificadoEm = verificadoEm;
        Verificado = verificado;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetAutorizador(string autorizador)
    {
        if (string.IsNullOrWhiteSpace(autorizador))
            throw new ArgumentException("Autorizador é obrigatório.", nameof(autorizador));
        if (autorizador.Trim().Length > 20)
            throw new ArgumentException("Autorizador excede 20 caracteres.", nameof(autorizador));
        Autorizador = autorizador.Trim().ToUpperInvariant();
    }

    private void SetVersaoServico(string versaoServico)
    {
        if (string.IsNullOrWhiteSpace(versaoServico))
            throw new ArgumentException("Versão do serviço é obrigatória.", nameof(versaoServico));
        if (versaoServico.Trim().Length > 20)
            throw new ArgumentException("Versão do serviço excede 20 caracteres.", nameof(versaoServico));
        VersaoServico = versaoServico.Trim();
    }

    private void SetUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL é obrigatória.", nameof(url));
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("URL deve ser HTTPS absoluta.", nameof(url));
        if (url.Trim().Length > 500)
            throw new ArgumentException("URL excede 500 caracteres.", nameof(url));
        Url = url.Trim();
    }
}
