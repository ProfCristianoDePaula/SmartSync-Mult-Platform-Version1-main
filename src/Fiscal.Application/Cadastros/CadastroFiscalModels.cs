using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;

namespace Fiscal.Application.Cadastros;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record ProdutoFiscalDto(
    Guid ProdutoId,
    TipoItemFiscal Tipo,
    string? Ncm,
    string? CfopDentroUf,
    string? CfopForaUf,
    bool ProntoNFe,
    bool ProntoNFCe,
    bool ProntoNFSe,
    IReadOnlyList<string> Pendencias);

public sealed record ClienteFiscalDto(
    Guid ClienteId,
    TipoPessoaFiscal TipoPessoa,
    string Documento,
    string Nome,
    IndicadorIe IndicadorIe,
    string? InscricaoEstadual,
    bool ConsumidorFinal);

public sealed record NaturezaOperacaoDto(
    Guid Id,
    string Codigo,
    string Descricao,
    TipoOperacaoFiscal TipoOperacao);

public sealed record ProdutoCsvRow(
    Guid ProdutoId,
    TipoItemFiscal Tipo,
    string? Ncm, string? Cest, string? Origem,
    string? UnCom, string? UnTrib, decimal? Fator, string? Gtin,
    string? CfopDentro, string? CfopFora,
    string? CstIcms, string? Csosn, decimal? AliqIcms,
    string? ItemLc116, string? Nbs, string? CodTrib, decimal? AliqIss);

public sealed record ProdutoCsvReport(
    int Total, int Criados, int Atualizados, int Rejeitados,
    IReadOnlyList<ProdutoCsvError> Erros);

public sealed record ProdutoCsvError(int Linha, string Erro);

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

public sealed record UpsertProdutoFiscalCommand(
    Guid ProdutoId,
    TipoItemFiscal Tipo,
    string? Ncm, string? Cest, string? Origem,
    string? UnCom, string? UnTrib, decimal? Fator, string? Gtin,
    string? CfopDentro, string? CfopFora,
    string? CstIcms, string? Csosn, decimal? AliqIcms,
    string? CstPis, string? CstCofins, decimal? AliqPis, decimal? AliqCofins,
    string? CstIpi, decimal? AliqIpi,
    string? CClassTrib, string? CstIbsCbs,
    string? ItemLc116, string? Nbs, string? CodTrib, decimal? AliqIss)
{
    public UpsertProdutoFiscalCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpsertClienteFiscalCommand(
    Guid ClienteId,
    TipoPessoaFiscal TipoPessoa,
    string Documento,
    string Nome,
    IndicadorIe IndicadorIe,
    string? InscricaoEstadual,
    string Street, string Number, string? Complement, string District,
    string City, string State, string PostalCode, string CodigoIbgeMunicipio,
    string? Email,
    bool ConsumidorFinal)
{
    public UpsertClienteFiscalCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record CreateNaturezaCommand(string Codigo, string Descricao, TipoOperacaoFiscal TipoOperacao)
{
    public CreateNaturezaCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

// ---------------------------------------------------------------------------
// Validators (só formato — P4: sem motor tributário)
// ---------------------------------------------------------------------------

public sealed class UpsertProdutoFiscalCommandValidator : AbstractValidator<UpsertProdutoFiscalCommand>
{
    public UpsertProdutoFiscalCommandValidator()
    {
        RuleFor(x => x.ProdutoId).NotEqual(Guid.Empty);
        RuleFor(x => x.Tipo).IsInEnum();
        When(x => x.Ncm is not null, () =>
            RuleFor(x => x.Ncm!).Must(n => n.Trim().Length == 8 && n.Trim().All(char.IsDigit))
                .WithMessage("NCM deve conter 8 dígitos."));
        When(x => x.Cest is not null, () =>
            RuleFor(x => x.Cest!).Must(c => c.Trim().Length == 7 && c.Trim().All(char.IsDigit))
                .WithMessage("CEST deve conter 7 dígitos."));
        When(x => x.CfopDentro is not null, () =>
            RuleFor(x => x.CfopDentro!).Must(CfopValido).WithMessage("CFOP dentro da UF inválido."));
        When(x => x.CfopFora is not null, () =>
            RuleFor(x => x.CfopFora!).Must(CfopValido).WithMessage("CFOP fora da UF inválido."));
    }

    internal static bool CfopValido(string? cfop)
    {
        if (cfop is null) return true;
        var c = cfop.Trim();
        return c.Length == 4 && c.All(char.IsDigit) && "123567".Contains(c[0]);
    }
}

public sealed class UpsertClienteFiscalCommandValidator : AbstractValidator<UpsertClienteFiscalCommand>
{
    public UpsertClienteFiscalCommandValidator()
    {
        RuleFor(x => x.ClienteId).NotEqual(Guid.Empty);
        RuleFor(x => x.TipoPessoa).IsInEnum();
        RuleFor(x => x.Documento).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(255);
        RuleFor(x => x.IndicadorIe).IsInEnum();
        RuleFor(x => x.State).Length(2);
        RuleFor(x => x.PostalCode).Must(p => new string((p ?? "").Where(char.IsDigit).ToArray()).Length == 8)
            .WithMessage("CEP deve conter 8 dígitos.");
        RuleFor(x => x.CodigoIbgeMunicipio).Must(c => new string((c ?? "").Where(char.IsDigit).ToArray()).Length == 7)
            .WithMessage("Código IBGE deve conter 7 dígitos.");
        When(x => x.IndicadorIe == IndicadorIe.Contribuinte, () =>
            RuleFor(x => x.InscricaoEstadual).NotEmpty()
                .WithMessage("IE obrigatória para contribuinte."));
    }
}

// ---------------------------------------------------------------------------
// Interfaces
// ---------------------------------------------------------------------------

public interface IProdutoFiscalService
{
    Task<ProdutoFiscalDto> UpsertAsync(UpsertProdutoFiscalCommand command, CancellationToken ct = default);
    Task<ProdutoFiscalDto?> GetAsync(TenantId tenantId, Guid produtoId, CancellationToken ct = default);
    Task<IReadOnlyList<ProdutoFiscalDto>> PendenciasAsync(TenantId tenantId, IReadOnlyList<Guid> produtoIds, CrtFiscal? crt, CancellationToken ct = default);
    Task<ProdutoCsvReport> ImportarCsvAsync(TenantId tenantId, Stream csv, CancellationToken ct = default);
}

public interface IClienteFiscalService
{
    Task<ClienteFiscalDto> UpsertAsync(UpsertClienteFiscalCommand command, CancellationToken ct = default);
    Task<ClienteFiscalDto?> GetAsync(TenantId tenantId, Guid clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ValidarAsync(TenantId tenantId, Guid clienteId, CancellationToken ct = default);
}

public interface INaturezaService
{
    Task<NaturezaOperacaoDto> CreateAsync(CreateNaturezaCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<NaturezaOperacaoDto>> ListAsync(TenantId tenantId, CancellationToken ct = default);
    Task<bool> DeleteAsync(TenantId tenantId, Guid id, CancellationToken ct = default);
}

/// <summary>Validador fiscal: "este produto/cliente está pronto?" (só formato + presença).</summary>
public interface IValidadorFiscal
{
    Task<IReadOnlyList<string>> ValidarProdutoAsync(
        TenantId tenantId, Guid produtoId, CrtFiscal? crt, TipoDocumentoFiscal doc,
        CancellationToken ct = default);
}
