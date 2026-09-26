using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using Fiscal.Domain.ValueObjects;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Perfil fiscal do cliente (liga `tenantId+clienteId` do Identity, sem FK
/// cross-DB e sem duplicar o login). Endereço com IBGE.
/// </summary>
public sealed class ClienteFiscal : Entity<ClienteFiscalId>
{
    public TenantId TenantId { get; private set; }
    public Guid ClienteId { get; private set; }
    public TipoPessoaFiscal TipoPessoa { get; private set; }
    public string Documento { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public IndicadorIe IndicadorIe { get; private set; }
    public string? InscricaoEstadual { get; private set; }
    public FiscalAddress Endereco { get; private set; } = null!;
    public string? Email { get; private set; }
    public bool ConsumidorFinal { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ClienteFiscal() { }

    private ClienteFiscal(
        ClienteFiscalId id, TenantId tenantId, Guid clienteId, TipoPessoaFiscal tipoPessoa,
        string documento, string nome, IndicadorIe indicadorIe, string? inscricaoEstadual,
        FiscalAddress endereco, string? email, bool consumidorFinal)
        : base(id)
    {
        TenantId = tenantId;
        ClienteId = clienteId;
        TipoPessoa = tipoPessoa;
        SetDocumento(tipoPessoa, documento);
        SetNome(nome);
        IndicadorIe = indicadorIe;
        InscricaoEstadual = string.IsNullOrWhiteSpace(inscricaoEstadual) ? null : inscricaoEstadual.Trim();
        Endereco = endereco;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        ConsumidorFinal = consumidorFinal;
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static ClienteFiscal Create(
        TenantId tenantId, Guid clienteId, TipoPessoaFiscal tipoPessoa,
        string documento, string nome, IndicadorIe indicadorIe, string? inscricaoEstadual,
        FiscalAddress endereco, string? email, bool consumidorFinal)
        => new(ClienteFiscalId.New(), tenantId, clienteId, tipoPessoa, documento,
            nome, indicadorIe, inscricaoEstadual, endereco, email, consumidorFinal);

    public void Update(
        TipoPessoaFiscal tipoPessoa, string documento, string nome,
        IndicadorIe indicadorIe, string? inscricaoEstadual,
        FiscalAddress endereco, string? email, bool consumidorFinal)
    {
        TipoPessoa = tipoPessoa;
        SetDocumento(tipoPessoa, documento);
        SetNome(nome);
        IndicadorIe = indicadorIe;
        InscricaoEstadual = string.IsNullOrWhiteSpace(inscricaoEstadual) ? null : inscricaoEstadual.Trim();
        Endereco = endereco;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        ConsumidorFinal = consumidorFinal;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetDocumento(TipoPessoaFiscal tipo, string documento)
    {
        var d = new string((documento ?? "").Where(char.IsDigit).ToArray());
        var esperado = tipo == TipoPessoaFiscal.Fisica ? 11 : 14;
        if (d.Length != esperado)
            throw new ArgumentException($"Documento deve conter {esperado} dígitos para {tipo}.", nameof(documento));
        Documento = d;
    }

    private void SetNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome/razão obrigatório.", nameof(nome));
        Nome = nome.Trim();
    }
}
