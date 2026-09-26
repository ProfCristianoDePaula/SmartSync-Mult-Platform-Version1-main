using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Situação NFS-e do município: adesão informada, parametrização conhecida e
/// modo de emissão. Override administrativo (`SobrescritoManual`) nunca é
/// sobrescrito por importação. Ausência de resposta ≠ não adesão (R3).
/// </summary>
public sealed class NfseMunicipioConfig : Entity<NfseMunicipioConfigId>
{
    public string CodigoIbge { get; private set; } = null!;
    public ModoEmissaoNfse Modo { get; private set; }
    public string? Fonte { get; private set; }
    public DateTime AtualizadoEm { get; private set; }
    public bool SobrescritoManual { get; private set; }
    public string? Observacao { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private NfseMunicipioConfig() { }

    private NfseMunicipioConfig(
        NfseMunicipioConfigId id, string codigoIbge, ModoEmissaoNfse modo,
        string? fonte, DateTime atualizadoEm, bool sobrescritoManual, string? observacao)
        : base(id)
    {
        SetCodigoIbge(codigoIbge);
        Modo = modo;
        Fonte = string.IsNullOrWhiteSpace(fonte) ? null : fonte.Trim();
        AtualizadoEm = atualizadoEm;
        SobrescritoManual = sobrescritoManual;
        SetObservacao(observacao);
        IsActive = true;
    }

    public static NfseMunicipioConfig Create(
        string codigoIbge, ModoEmissaoNfse modo, string? fonte,
        bool sobrescritoManual = false, string? observacao = null)
        => new(NfseMunicipioConfigId.New(), codigoIbge, modo, fonte,
            DateTime.UtcNow, sobrescritoManual, observacao);

    /// <summary>Aplica dados da importação — nunca sobre manual (chamador garante).</summary>
    public void AplicarImportacao(ModoEmissaoNfse modo, string? fonte, string? observacao)
    {
        Modo = modo;
        Fonte = string.IsNullOrWhiteSpace(fonte) ? null : fonte.Trim();
        SetObservacao(observacao);
        AtualizadoEm = DateTime.UtcNow;
    }

    public void AplicarOverrideManual(ModoEmissaoNfse modo, string? observacao, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Motivo do override é obrigatório.", nameof(motivo));
        Modo = modo;
        SetObservacao(observacao);
        SobrescritoManual = true;
        AtualizadoEm = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetCodigoIbge(string codigoIbge)
    {
        var digits = new string((codigoIbge ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 7)
            throw new ArgumentException("Código IBGE deve conter 7 dígitos.", nameof(codigoIbge));
        CodigoIbge = digits;
    }

    private void SetObservacao(string? observacao)
    {
        if (observacao is not null && observacao.Trim().Length > 500)
            throw new ArgumentException("Observação excede 500 caracteres.", nameof(observacao));
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }
}
