using Fiscal.Api.Extensions;
using Fiscal.Application.Certificados;
using Fiscal.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Certificados A1: upload com senha (write-only), leitura só de metadados,
/// revogação com histórico. PFX/senha nunca em resposta, log ou exceção (R4).
/// </summary>
[ApiController]
[Route("api/fiscal/certificados")]
[Authorize(Policy = "tenant")]
[Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager}")]
public sealed class CertificadosController : FiscalControllerBase
{
    private const long MaxArquivoBytes = 1 * 1024 * 1024;
    private readonly ICertificadoService _service;

    public CertificadosController(ICertificadoService service) => _service = service;

    [HttpPost]
    [RequestSizeLimit(MaxArquivoBytes)]
    [ProducesResponseType(typeof(CertificadoDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile arquivo, [FromForm] string senha,
        [FromForm] Guid? branchId, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0 || arquivo.Length > MaxArquivoBytes)
            return BusinessRuleFailure(new BusinessRuleViolationException("Arquivo PFX/P12 obrigatório (≤1 MB)."));

        byte[] bytes;
        await using (var ms = new MemoryStream())
        {
            await arquivo.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        try
        {
            var dto = await _service.UploadAsync(
                new UploadCertificadoCommand(bytes, arquivo.FileName, senha, branchId)
                    .WithTenant(User.GetRequiredTenantId()),
                User.GetUserId(), UserRole(), ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
        finally
        {
            CryptographicOperations_Zero(bytes);
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _service.ListAsync(User.GetRequiredTenantId(), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _service.GetByIdAsync(User.GetRequiredTenantId(), id, ct);
        return dto is null ? NotFound("Certificado não encontrado.") : Ok(dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revogar(Guid id, CancellationToken ct)
    {
        var ok = await _service.RevogarAsync(User.GetRequiredTenantId(), id, User.GetUserId(), ct);
        return ok ? NoContent() : NotFound("Certificado não encontrado.");
    }

    private string UserRole()
        => User.FindFirst(Fiscal.Domain.Common.JwtClaims.Role)?.Value ?? "";

    private static void CryptographicOperations_Zero(byte[] bytes)
    {
        if (bytes is not null) Array.Clear(bytes);
    }
}
