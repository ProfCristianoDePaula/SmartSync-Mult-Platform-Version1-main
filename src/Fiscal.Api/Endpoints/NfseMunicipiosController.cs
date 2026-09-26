using Fiscal.Api.Extensions;
using Fiscal.Application.Nfse;
using Fiscal.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Municípios e situação NFS-e: importação e override exclusivos do
/// SuperAdmin; leitura de qualquer role do tenant. Uploads limitados a
/// 5 MB; nunca SSRF (arquivo vem no corpo, não via URL — R8).
/// </summary>
[ApiController]
[Route("api/fiscal/nfse/municipios")]
public sealed class NfseMunicipiosController : FiscalControllerBase
{
    private const long MaxArquivoBytes = 5 * 1024 * 1024;
    private readonly INfseMunicipioService _service;

    public NfseMunicipiosController(INfseMunicipioService service) => _service = service;

    [HttpPost("importar")]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [RequestSizeLimit(MaxArquivoBytes)]
    [ProducesResponseType(typeof(NfseImportReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> Importar(
        IFormFile arquivo, [FromQuery] bool ignorarMinimo = false, CancellationToken ct = default)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BusinessRuleFailure(new BusinessRuleViolationException("Arquivo vazio."));
        if (arquivo.Length > MaxArquivoBytes)
            return BusinessRuleFailure(new BusinessRuleViolationException("Arquivo excede 5 MB."));

        try
        {
            await using var stream = arquivo.OpenReadStream();
            using var copia = new MemoryStream();
            await stream.CopyToAsync(copia, ct);
            copia.Position = 0;
            var report = await _service.ImportarCsvAsync(copia, arquivo.FileName, User.GetUserId(), ignorarMinimo, ct);
            return Ok(report);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{ibge:length(7)}/situacao")]
    [Authorize(Policy = "tenant")]
    [ProducesResponseType(typeof(NfseSituacaoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Situacao(string ibge, CancellationToken ct)
    {
        var dto = await _service.ObterSituacaoAsync(ibge, ct);
        return dto is null ? NotFound("Município não cadastrado.") : Ok(dto);
    }

    [HttpPut("{ibge:length(7)}")]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    public async Task<IActionResult> Override(string ibge, [FromBody] OverrideBody body, CancellationToken ct)
    {
        try
        {
            var dto = await _service.OverrideManualAsync(
                new NfseManualOverrideCommand(ibge, body.Modo, body.Observacao, body.Motivo),
                User.GetUserId(), ct);
            return Ok(dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{ibge:length(7)}/ambientes")]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    public async Task<IActionResult> UpsertAmbiente(
        string ibge, [FromBody] NfseAmbienteUpsertCommand body, CancellationToken ct)
    {
        try
        {
            var dto = await _service.UpsertAmbienteAsync(body with { CodigoIbge = ibge }, User.GetUserId(), ct);
            return Ok(dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet]
    [Authorize(Policy = "tenant")]
    public async Task<IActionResult> List(
        [FromQuery] string? uf, [FromQuery] int? modo, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        try
        {
            var modoEnum = modo is null ? null : (Fiscal.Domain.Enums.ModoEmissaoNfse?)modo.Value;
            return Ok(await _service.ListarAsync(new ListMunicipiosQuery(uf, modoEnum, search, page, pageSize), ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
    }

    public sealed record OverrideBody(Fiscal.Domain.Enums.ModoEmissaoNfse Modo, string? Observacao, string Motivo);
}
