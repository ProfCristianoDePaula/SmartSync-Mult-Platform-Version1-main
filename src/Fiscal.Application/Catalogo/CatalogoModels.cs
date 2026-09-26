using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;

namespace Fiscal.Application.Catalogo;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record UfFiscalDto(
    Guid Id,
    string Sigla,
    int CodigoIbge,
    string Nome,
    AutorizadorTipo AutorizadorNFe,
    AutorizadorTipo AutorizadorNFCe,
    bool IsActive);

public sealed record SefazEndpointDto(
    Guid Id,
    string Autorizador,
    ModeloFiscal Modelo,
    SefazServico Servico,
    AmbienteFiscal Ambiente,
    string VersaoServico,
    string Url,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim,
    string? FonteUrl,
    DateTime? VerificadoEm,
    bool Verificado,
    bool IsActive);

public sealed record UfAmbientesDto(
    UfFiscalDto Uf,
    IReadOnlyList<SefazEndpointDto> NFeHomologacao,
    IReadOnlyList<SefazEndpointDto> NFeProducao,
    IReadOnlyList<SefazEndpointDto> NFCeHomologacao,
    IReadOnlyList<SefazEndpointDto> NFCeProducao);

public sealed record SefazImportRow(
    string Autorizador,
    int Modelo,
    int Servico,
    int Ambiente,
    string VersaoServico,
    string Url,
    DateTime? VigenciaInicio = null,
    DateTime? VigenciaFim = null,
    string? FonteUrl = null);

public sealed record SefazImportRequest(
    string Versao,
    IReadOnlyList<SefazImportRow> Endpoints);

public sealed record SefazImportError(int Linha, string Erro);

public sealed record SefazImportReport(
    int Total,
    int Criados,
    int Atualizados,
    int Inalterados,
    int Rejeitados,
    IReadOnlyList<SefazImportError> Erros);

// ---------------------------------------------------------------------------
// UF — Commands
// ---------------------------------------------------------------------------

public sealed record CreateUfCommand(
    string Sigla,
    int CodigoIbge,
    string Nome,
    AutorizadorTipo AutorizadorNFe,
    AutorizadorTipo AutorizadorNFCe);

public sealed record UpdateUfCommand(
    string Sigla,
    int CodigoIbge,
    string Nome,
    AutorizadorTipo AutorizadorNFe,
    AutorizadorTipo AutorizadorNFCe);

// ---------------------------------------------------------------------------
// Endpoint — Commands
// ---------------------------------------------------------------------------

public sealed record CreateEndpointCommand(
    string Autorizador,
    ModeloFiscal Modelo,
    SefazServico Servico,
    AmbienteFiscal Ambiente,
    string VersaoServico,
    string Url,
    DateTime? VigenciaInicio = null,
    DateTime? VigenciaFim = null,
    string? FonteUrl = null,
    DateTime? VerificadoEm = null,
    bool Verificado = false);

public sealed record UpdateEndpointCommand(
    Guid Id,
    string VersaoServico,
    string Url,
    DateTime? VigenciaInicio = null,
    DateTime? VigenciaFim = null,
    string? FonteUrl = null,
    DateTime? VerificadoEm = null,
    bool Verificado = false);

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class CreateUfCommandValidator : AbstractValidator<CreateUfCommand>
{
    public CreateUfCommandValidator()
    {
        RuleFor(x => x.Sigla).NotEmpty().Length(2).WithMessage("Sigla da UF deve conter 2 letras.");
        RuleFor(x => x.CodigoIbge).InclusiveBetween(11, 53).WithMessage("cUF inválido.");
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AutorizadorNFe).IsInEnum();
        RuleFor(x => x.AutorizadorNFCe).IsInEnum();
    }
}

public sealed class UpdateUfCommandValidator : AbstractValidator<UpdateUfCommand>
{
    public UpdateUfCommandValidator()
    {
        RuleFor(x => x.Sigla).NotEmpty().Length(2);
        RuleFor(x => x.CodigoIbge).InclusiveBetween(11, 53);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AutorizadorNFe).IsInEnum();
        RuleFor(x => x.AutorizadorNFCe).IsInEnum();
    }
}

public sealed class CreateEndpointCommandValidator : AbstractValidator<CreateEndpointCommand>
{
    public CreateEndpointCommandValidator()
    {
        RuleFor(x => x.Autorizador).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Modelo).IsInEnum().Must(m => m is ModeloFiscal.NFe55 or ModeloFiscal.NFCe65)
            .WithMessage("Modelo deve ser 55 (NF-e) ou 65 (NFC-e).");
        RuleFor(x => x.Servico).IsInEnum();
        RuleFor(x => x.Ambiente).IsInEnum();
        RuleFor(x => x.VersaoServico).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
            .WithMessage("URL deve ser HTTPS absoluta.");
    }
}

public sealed class UpdateEndpointCommandValidator : AbstractValidator<UpdateEndpointCommand>
{
    public UpdateEndpointCommandValidator()
    {
        RuleFor(x => x.Id).NotEqual(Guid.Empty);
        RuleFor(x => x.VersaoServico).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
            .WithMessage("URL deve ser HTTPS absoluta.");
    }
}

// ---------------------------------------------------------------------------
// Service interfaces
// ---------------------------------------------------------------------------

public interface IUfFiscalService
{
    Task<UfFiscalDto> CreateAsync(CreateUfCommand command, Guid userId, CancellationToken ct = default);
    Task<UfFiscalDto?> UpdateAsync(UpdateUfCommand command, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(string sigla, Guid userId, CancellationToken ct = default);
    Task<UfFiscalDto?> GetBySiglaAsync(string sigla, CancellationToken ct = default);
    Task<IReadOnlyList<UfFiscalDto>> ListAsync(CancellationToken ct = default);
    Task<UfAmbientesDto?> GetAmbientesAsync(string sigla, CancellationToken ct = default);
}

public interface ISefazEndpointService
{
    Task<SefazEndpointDto> CreateAsync(CreateEndpointCommand command, Guid userId, CancellationToken ct = default);
    Task<SefazEndpointDto?> UpdateAsync(UpdateEndpointCommand command, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<SefazEndpointDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SefazEndpointDto>> ListAsync(CancellationToken ct = default);
    Task<SefazImportReport> ImportAsync(SefazImportRequest request, Guid userId, CancellationToken ct = default);
}
