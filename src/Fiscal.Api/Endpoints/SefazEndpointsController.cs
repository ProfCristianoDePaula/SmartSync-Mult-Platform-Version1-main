using Fiscal.Api.Extensions;
using Fiscal.Application.Catalogo;
using Fiscal.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Catálogo de endpoints SEFAZ: CRUD e importação exclusivos do SuperAdmin.
/// TenantAdmin/Manager consultam serviços via GET /api/fiscal/ufs/{uf}/ambientes.
/// URLs de envio vêm SOMENTE daqui (R8).
/// </summary>
[ApiController]
[Route("api/fiscal/sefaz-endpoints")]
[Authorize(Roles = PlatformRoles.SuperAdmin)]
public sealed class SefazEndpointsController : FiscalControllerBase
{
    private readonly ISefazEndpointService _endpoints;

    public SefazEndpointsController(ISefazEndpointService endpoints) => _endpoints = endpoints;

    [HttpPost]
    [ProducesResponseType(typeof(SefazEndpointDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateEndpointCommand command, CancellationToken ct)
    {
        try
        {
            var endpoint = await _endpoints.CreateAsync(command, User.GetUserId(), ct);
            return CreatedAtAction(nameof(GetById), new { id = endpoint.Id }, endpoint);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEndpointCommand body, CancellationToken ct)
    {
        try
        {
            var endpoint = await _endpoints.UpdateAsync(body with { Id = id }, User.GetUserId(), ct);
            return endpoint is null ? NotFound("Endpoint não encontrado.") : Ok(endpoint);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _endpoints.DeleteAsync(id, User.GetUserId(), ct);
        return ok ? NoContent() : NotFound("Endpoint não encontrado.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var endpoint = await _endpoints.GetByIdAsync(id, ct);
        return endpoint is null ? NotFound("Endpoint não encontrado.") : Ok(endpoint);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _endpoints.ListAsync(ct));

    /// <summary>
    /// Importação idempotente de pacote JSON versionado (R3: cada linha informa
    /// a fonte; linhas inválidas são rejeitadas com erro por linha).
    /// </summary>
    [HttpPost("importar")]
    [ProducesResponseType(typeof(SefazImportReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromBody] SefazImportRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _endpoints.ImportAsync(request, User.GetUserId(), ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }
}
