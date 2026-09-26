using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;

namespace Fiscal.Domain.Entities;

/// <summary>
/// Perfil fiscal do produto (liga `tenantId+produtoId` do Estoque, sem FK
/// cross-DB e sem duplicar o cadastro). Códigos como texto (zeros preservados).
/// Sem motor tributário (P4): valores INFORMADOS, só validação formal.
/// </summary>
public sealed class ProdutoFiscal : Entity<ProdutoFiscalId>
{
    public TenantId TenantId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public TipoItemFiscal Tipo { get; private set; }
    public string? Ncm { get; private set; }
    public string? Cest { get; private set; }
    public string? Origem { get; private set; }
    public string? UnidadeComercial { get; private set; }
    public string? UnidadeTributavel { get; private set; }
    public decimal? FatorConversao { get; private set; }
    public string? Gtin { get; private set; }
    public string? CfopDentroUf { get; private set; }
    public string? CfopForaUf { get; private set; }
    public string? CstIcms { get; private set; }
    public string? Csosn { get; private set; }
    public decimal? AliquotaIcms { get; private set; }
    public string? CstPis { get; private set; }
    public string? CstCofins { get; private set; }
    public decimal? AliquotaPis { get; private set; }
    public decimal? AliquotaCofins { get; private set; }
    public string? CstIpi { get; private set; }
    public decimal? AliquotaIpi { get; private set; }
    public string? CClassTrib { get; private set; }
    public string? CstIbsCbs { get; private set; }
    public string? ItemLc116 { get; private set; }
    public string? Nbs { get; private set; }
    public string? CodigoTribNacional { get; private set; }
    public decimal? AliquotaIss { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ProdutoFiscal() { }

    private ProdutoFiscal(ProdutoFiscalId id, TenantId tenantId, Guid produtoId, TipoItemFiscal tipo)
        : base(id)
    {
        TenantId = tenantId;
        ProdutoId = produtoId;
        Tipo = tipo;
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static ProdutoFiscal Create(TenantId tenantId, Guid produtoId, TipoItemFiscal tipo)
        => new(ProdutoFiscalId.New(), tenantId, produtoId, tipo);

    public void DefinirTipo(TipoItemFiscal tipo)
    {
        Tipo = tipo;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void DefinirMercadoria(
        string? ncm, string? cest, string? origem, string? unCom, string? unTrib,
        decimal? fator, string? gtin, string? cfopDentro, string? cfopFora,
        string? cstIcms, string? csosn, decimal? aliqIcms,
        string? cstPis, string? cstCofins, decimal? aliqPis, decimal? aliqCofins,
        string? cstIpi, decimal? aliqIpi, string? cClassTrib, string? cstIbsCbs)
    {
        Ncm = Norm(ncm, 8, false); Cest = Norm(cest, 7, false); Origem = Norm(origem, 1, false);
        UnidadeComercial = Norm(unCom, 6, false); UnidadeTributavel = Norm(unTrib, 6, false);
        FatorConversao = fator; Gtin = Norm(gtin, 14, true);
        CfopDentroUf = Norm(cfopDentro, 4, false); CfopForaUf = Norm(cfopFora, 4, false);
        CstIcms = Norm(cstIcms, 3, false); Csosn = Norm(csosn, 3, false);
        AliquotaIcms = aliqIcms; CstPis = Norm(cstPis, 2, false); CstCofins = Norm(cstCofins, 2, false);
        AliquotaPis = aliqPis; AliquotaCofins = aliqCofins;
        CstIpi = Norm(cstIpi, 2, false); AliquotaIpi = aliqIpi;
        CClassTrib = Norm(cClassTrib, 6, false); CstIbsCbs = Norm(cstIbsCbs, 3, false);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void DefinirServico(
        string? itemLc116, string? nbs, string? codigoTribNacional,
        decimal? aliquotaIss, string? cfopDentro, string? cfopFora)
    {
        ItemLc116 = Norm(itemLc116, 10, false); Nbs = Norm(nbs, 10, false);
        CodigoTribNacional = Norm(codigoTribNacional, 10, false);
        AliquotaIss = aliquotaIss;
        CfopDentroUf = Norm(cfopDentro, 4, false); CfopForaUf = Norm(cfopFora, 4, false);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (!IsActive) return;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
    }

    private static string? Norm(string? v, int max, bool allowSemGtin)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        v = v.Trim();
        if (allowSemGtin && v.Equals("SEM GTIN", StringComparison.OrdinalIgnoreCase)) return "SEM GTIN";
        if (v.Length > max) throw new ArgumentException($"Campo excede {max} caracteres: {v}.");
        return v;
    }
}
