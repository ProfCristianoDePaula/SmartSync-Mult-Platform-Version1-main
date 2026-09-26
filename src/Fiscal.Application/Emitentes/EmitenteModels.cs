using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;

namespace Fiscal.Application.Emitentes;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record EnderecoFiscalDto(
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State,
    string PostalCode,
    string CodigoIbgeMunicipio);

public sealed record EmitenteDto(
    Guid Id,
    Guid BranchId,
    string Cnpj,
    bool CnpjAlfanumerico,
    string RazaoSocial,
    string Fantasia,
    string? InscricaoEstadual,
    string? InscricaoMunicipal,
    string? Cnae,
    CrtFiscal Crt,
    EnderecoFiscalDto Endereco,
    string Telefone,
    string Email,
    bool EmissaoAutomaticaVenda,
    bool IsActive);

public sealed record ConfiguracaoDocumentoDto(
    Guid Id,
    Guid EmitenteId,
    TipoDocumentoFiscal Tipo,
    AmbienteFiscal Ambiente,
    bool Habilitado,
    string Serie,
    ModoIntegracao ModoIntegracao,
    Guid? ReferenciaCertificado,
    string? CscId);

public sealed record ProntidaoItemDto(string Item, bool Ok, string Detalhe);

public sealed record ProntidaoDto(
    Guid EmitenteId,
    TipoDocumentoFiscal Tipo,
    AmbienteFiscal Ambiente,
    bool Pronto,
    IReadOnlyList<ProntidaoItemDto> Itens);

public sealed record ConcessaoDto(
    Guid Id,
    Guid BranchId,
    Guid UserId,
    PapelUnidade Papel);

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

public sealed record CreateEmitenteCommand(
    Guid BranchId,
    string Cnpj,
    string RazaoSocial,
    string Fantasia,
    string? InscricaoEstadual,
    string? InscricaoMunicipal,
    string? Cnae,
    CrtFiscal Crt,
    EnderecoFiscalDto Endereco,
    string Telefone,
    string Email)
{
    public CreateEmitenteCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpdateEmitenteCommand(
    Guid EmitenteId,
    string RazaoSocial,
    string Fantasia,
    string? InscricaoEstadual,
    string? InscricaoMunicipal,
    string? Cnae,
    CrtFiscal Crt,
    EnderecoFiscalDto Endereco,
    string Telefone,
    string Email)
{
    public UpdateEmitenteCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record UpsertConfiguracaoCommand(
    Guid EmitenteId,
    TipoDocumentoFiscal Tipo,
    AmbienteFiscal Ambiente,
    bool Habilitado,
    string Serie,
    ModoIntegracao ModoIntegracao,
    Guid? ReferenciaCertificado = null,
    string? CscId = null)
{
    public UpsertConfiguracaoCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

public sealed record PromoverProducaoCommand(
    Guid EmitenteId,
    TipoDocumentoFiscal Tipo,
    string TextoConfirmacao)
{
    public PromoverProducaoCommand WithContext(TenantId tenantId, Guid userId, string role)
        => this with { TenantId = tenantId, UserId = userId, Role = role };
    public TenantId TenantId { get; init; }
    public Guid UserId { get; init; }
    public string Role { get; init; } = "";
}

public sealed record GrantConcessaoCommand(
    Guid BranchId,
    Guid UserId,
    PapelUnidade Papel)
{
    public GrantConcessaoCommand WithTenant(TenantId tenantId) => this with { TenantId = tenantId };
    public TenantId TenantId { get; init; }
}

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class EnderecoFiscalDtoValidator : AbstractValidator<EnderecoFiscalDto>
{
    public EnderecoFiscalDtoValidator()
    {
        RuleFor(x => x.Street).NotEmpty();
        RuleFor(x => x.Number).NotEmpty();
        RuleFor(x => x.District).NotEmpty();
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.State).Length(2);
        RuleFor(x => x.PostalCode).Must(p => new string((p ?? "").Where(char.IsDigit).ToArray()).Length == 8)
            .WithMessage("CEP deve conter 8 dígitos.");
        RuleFor(x => x.CodigoIbgeMunicipio).Must(c => new string((c ?? "").Where(char.IsDigit).ToArray()).Length == 7)
            .WithMessage("Código IBGE deve conter 7 dígitos.");
    }
}

public sealed class CreateEmitenteCommandValidator : AbstractValidator<CreateEmitenteCommand>
{
    public CreateEmitenteCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEqual(Guid.Empty);
        RuleFor(x => x.Cnpj).NotEmpty().WithMessage("CNPJ do emitente é obrigatório.");
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Fantasia).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Crt).IsInEnum();
        RuleFor(x => x.Endereco).NotNull().SetValidator(new EnderecoFiscalDtoValidator());
        RuleFor(x => x.Telefone).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class UpdateEmitenteCommandValidator : AbstractValidator<UpdateEmitenteCommand>
{
    public UpdateEmitenteCommandValidator()
    {
        RuleFor(x => x.EmitenteId).NotEqual(Guid.Empty);
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Fantasia).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Crt).IsInEnum();
        RuleFor(x => x.Endereco).NotNull().SetValidator(new EnderecoFiscalDtoValidator());
        RuleFor(x => x.Telefone).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class UpsertConfiguracaoCommandValidator : AbstractValidator<UpsertConfiguracaoCommand>
{
    public UpsertConfiguracaoCommandValidator()
    {
        RuleFor(x => x.EmitenteId).NotEqual(Guid.Empty);
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.Ambiente).IsInEnum();
        RuleFor(x => x.Serie).NotEmpty().MaximumLength(3);
        RuleFor(x => x.ModoIntegracao).IsInEnum();
    }
}

// ---------------------------------------------------------------------------
// Service interfaces
// ---------------------------------------------------------------------------

public interface IEmitenteFiscalService
{
    Task<EmitenteDto> CreateAsync(CreateEmitenteCommand command, Guid userId, string role, CancellationToken ct = default);
    Task<EmitenteDto?> UpdateAsync(UpdateEmitenteCommand command, Guid userId, string role, CancellationToken ct = default);
    Task<EmitenteDto?> GetByIdAsync(TenantId tenantId, Guid emitenteId, CancellationToken ct = default);
    Task<EmitenteDto?> GetByBranchAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default);
    Task<EmitenteDto> DefinirEmissaoAutomaticaAsync(TenantId tenantId, Guid emitenteId, bool ativa, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<EmitenteDto>> ListAsync(TenantId tenantId, CancellationToken ct = default);
    Task<ConfiguracaoDocumentoDto> UpsertConfiguracaoAsync(UpsertConfiguracaoCommand command, Guid userId, string role, CancellationToken ct = default);
    Task<ProntidaoDto> GetProntidaoAsync(TenantId tenantId, Guid emitenteId, TipoDocumentoFiscal tipo, AmbienteFiscal ambiente, CancellationToken ct = default);
    Task<ProntidaoDto> PromoverProducaoAsync(PromoverProducaoCommand command, CancellationToken ct = default);
    Task<TesteConexaoDto> TestarConexaoAsync(TenantId tenantId, Guid emitenteId, Guid userId, string role, CancellationToken ct = default);
}

public sealed record TesteConexaoDto(
    bool Online,
    string? CStat,
    string? Motivo,
    DateTime? HomologacaoValidadaEm);

public interface IConcessaoService
{
    Task<ConcessaoDto> GrantAsync(GrantConcessaoCommand command, Guid grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(TenantId tenantId, Guid concessaoId, CancellationToken ct = default);
    Task<IReadOnlyList<ConcessaoDto>> ListAsync(TenantId tenantId, Guid? branchId = null, CancellationToken ct = default);
    Task<bool> TemAcessoAsync(TenantId tenantId, BranchId branchId, Guid userId, PapelUnidade papel, CancellationToken ct = default);
}
