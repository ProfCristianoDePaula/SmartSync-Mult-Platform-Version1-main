using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>Natureza da operação por tenant (venda, devolução etc.).</summary>
public sealed class NaturezaOperacao : Entity<NaturezaOperacaoId>
{
    public TenantId TenantId { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public TipoOperacaoFiscal TipoOperacao { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    private NaturezaOperacao() { }

    private NaturezaOperacao(NaturezaOperacaoId id, TenantId tenantId, string codigo, string descricao, TipoOperacaoFiscal tipo)
        : base(id)
    {
        TenantId = tenantId;
        SetCodigo(codigo);
        SetDescricao(descricao);
        TipoOperacao = tipo;
        IsActive = true;
    }

    public static NaturezaOperacao Create(TenantId tenantId, string codigo, string descricao, TipoOperacaoFiscal tipo)
        => new(NaturezaOperacaoId.New(), tenantId, codigo, descricao, tipo);

    public void Update(string descricao, TipoOperacaoFiscal tipo)
    {
        SetDescricao(descricao);
        TipoOperacao = tipo;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private void SetCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("Código obrigatório.", nameof(codigo));
        if (codigo.Trim().Length > 20) throw new ArgumentException("Código excede 20 caracteres.", nameof(codigo));
        Codigo = codigo.Trim().ToUpperInvariant();
    }

    private void SetDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao)) throw new ArgumentException("Descrição obrigatória.", nameof(descricao));
        Descricao = descricao.Trim();
    }
}
