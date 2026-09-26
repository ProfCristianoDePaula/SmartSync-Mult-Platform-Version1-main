using Fiscal.Domain.Enums;
using FluentValidation;

namespace Fiscal.Application.Nfse;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public sealed record MunicipioDto(
    Guid Id,
    string CodigoIbge,
    string Nome,
    string Uf,
    bool IsActive);

public sealed record NfseMunicipioConfigDto(
    string CodigoIbge,
    ModoEmissaoNfse Modo,
    string? Fonte,
    DateTime AtualizadoEm,
    bool SobrescritoManual,
    string? Observacao);

public sealed record NfseAmbienteDto(
    Guid Id,
    string CodigoIbge,
    ModoEmissaoNfse Modo,
    AmbienteFiscal Ambiente,
    string? BaseUrlSefin,
    string? BaseUrlAdn,
    string? BaseUrlParametros,
    bool Verificado);

public sealed record NfseSituacaoDto(
    MunicipioDto Municipio,
    NfseMunicipioConfigDto Config,
    IReadOnlyList<NfseAmbienteDto> Ambientes,
    string Mensagem);

public sealed record NfseImportError(int Linha, string Erro);

public sealed record NfseImportReport(
    int Total,
    int Criados,
    int Atualizados,
    int IgnoradosManuais,
    int Rejeitados,
    string ArquivoHash,
    IReadOnlyList<NfseImportError> Erros);

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

public sealed record NfseManualOverrideCommand(
    string CodigoIbge,
    ModoEmissaoNfse Modo,
    string? Observacao,
    string Motivo);

public sealed record NfseAmbienteUpsertCommand(
    string CodigoIbge,
    ModoEmissaoNfse Modo,
    AmbienteFiscal Ambiente,
    string? BaseUrlSefin,
    string? BaseUrlAdn,
    string? BaseUrlParametros,
    bool Verificado);

public sealed record ListMunicipiosQuery(
    string? Uf = null,
    ModoEmissaoNfse? Modo = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

// ---------------------------------------------------------------------------
// Validators
// ---------------------------------------------------------------------------

public sealed class NfseManualOverrideCommandValidator : AbstractValidator<NfseManualOverrideCommand>
{
    public NfseManualOverrideCommandValidator()
    {
        RuleFor(x => x.CodigoIbge).NotEmpty().Must(c => new string(c.Where(char.IsDigit).ToArray()).Length == 7)
            .WithMessage("Código IBGE deve conter 7 dígitos.");
        RuleFor(x => x.Modo).IsInEnum();
        RuleFor(x => x.Observacao).MaximumLength(500);
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Motivo do override é obrigatório.");
    }
}

public sealed class NfseAmbienteUpsertCommandValidator : AbstractValidator<NfseAmbienteUpsertCommand>
{
    public NfseAmbienteUpsertCommandValidator()
    {
        RuleFor(x => x.CodigoIbge).NotEmpty()
            .Must(c => c.Trim() == "*" || new string(c.Where(char.IsDigit).ToArray()).Length == 7)
            .WithMessage("Código IBGE deve conter 7 dígitos ou '*' (padrão do modo).");
        RuleFor(x => x.Modo).IsInEnum();
        RuleFor(x => x.Ambiente).IsInEnum();
        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            foreach (var url in new[] { cmd.BaseUrlSefin, cmd.BaseUrlAdn, cmd.BaseUrlParametros })
            {
                if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps))
                    ctx.AddFailure($"URL deve ser HTTPS absoluta: {url}.");
            }
        });
    }
}

public sealed class ListMunicipiosQueryValidator : AbstractValidator<ListMunicipiosQuery>
{
    public ListMunicipiosQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

// ---------------------------------------------------------------------------
// Parametrização (consulta ao contribuinte; falha => Desconhecido)
// ---------------------------------------------------------------------------

public sealed record ConvenioResult(bool Conhecido, string? Situacao);

public sealed record ParametrosResult(bool Conhecido, string? Detalhes);

public interface INfseParametrizacaoClient
{
    Task<ConvenioResult> ObterConvenioAsync(string baseUrl, string codigoIbge, CancellationToken ct = default);
    Task<ParametrosResult> ObterParametrosAsync(string baseUrl, string codigoIbge, string? codigoServico, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Service interface
// ---------------------------------------------------------------------------

public interface INfseMunicipioService
{
    Task<NfseImportReport> ImportarCsvAsync(Stream csv, string nomeArquivo, Guid userId, bool ignorarMinimo = false, CancellationToken ct = default);
    Task<NfseSituacaoDto?> ObterSituacaoAsync(string codigoIbge, CancellationToken ct = default);
    Task<NfseMunicipioConfigDto> OverrideManualAsync(NfseManualOverrideCommand command, Guid userId, CancellationToken ct = default);
    Task<NfseAmbienteDto> UpsertAmbienteAsync(NfseAmbienteUpsertCommand command, Guid userId, CancellationToken ct = default);
    Task<PagedNfseMunicipios> ListarAsync(ListMunicipiosQuery query, CancellationToken ct = default);
}

public sealed record PagedNfseMunicipios(
    IReadOnlyList<NfseMunicipioListItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record NfseMunicipioListItem(
    string CodigoIbge,
    string Nome,
    string Uf,
    ModoEmissaoNfse Modo,
    bool SobrescritoManual);
