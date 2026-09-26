using Fiscal.Api.Extensions;
using Fiscal.Application.Catalogo;
using Fiscal.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Catálogo de UFs: escrita exclusiva do SuperAdmin; leitura de qualquer role
/// do tenant (TenantAdmin/Manager escolhem UF e ambientes — R2/R5).
/// </summary>
[ApiController]
[Route("api/fiscal/ufs")]
public sealed class UfsFiscaisController : FiscalControllerBase
{
    private readonly IUfFiscalService _ufs;

    public UfsFiscaisController(IUfFiscalService ufs) => _ufs = ufs;

    [HttpPost]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    [ProducesResponseType(typeof(UfFiscalDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateUfCommand command, CancellationToken ct)
    {
        try
        {
            var uf = await _ufs.CreateAsync(command, User.GetUserId(), ct);
            return CreatedAtAction(nameof(GetBySigla), new { sigla = uf.Sigla }, uf);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{sigla:length(2)}")]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    public async Task<IActionResult> Update(string sigla, [FromBody] UpdateUfCommand body, CancellationToken ct)
    {
        try
        {
            var uf = await _ufs.UpdateAsync(body with { Sigla = sigla }, User.GetUserId(), ct);
            return uf is null ? NotFound("UF não encontrada.") : Ok(uf);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{sigla:length(2)}")]
    [Authorize(Roles = PlatformRoles.SuperAdmin)]
    public async Task<IActionResult> Delete(string sigla, CancellationToken ct)
    {
        var ok = await _ufs.DeleteAsync(sigla, User.GetUserId(), ct);
        return ok ? NoContent() : NotFound("UF não encontrada.");
    }

    [HttpGet]
    [Authorize(Policy = "tenant")]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _ufs.ListAsync(ct));

    [HttpGet("{sigla:length(2)}")]
    [Authorize(Policy = "tenant")]
    public async Task<IActionResult> GetBySigla(string sigla, CancellationToken ct)
    {
        var uf = await _ufs.GetBySiglaAsync(sigla, ct);
        return uf is null ? NotFound("UF não encontrada.") : Ok(uf);
    }

    [HttpGet("{sigla:length(2)}/ambientes")]
    [Authorize(Policy = "tenant")]
    [ProducesResponseType(typeof(UfAmbientesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAmbientes(string sigla, CancellationToken ct)
    {
        var dto = await _ufs.GetAmbientesAsync(sigla, ct);
        return dto is null ? NotFound("UF não encontrada.") : Ok(dto);
    }
}
