using Fiscal.Api.Extensions;
using Fiscal.Application.Cadastros;
using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Cadastros fiscais (Fiscal-6): TenantAdmin/Manager escrevem; qualquer role
/// do tenant lê. Sem motor tributário: valores informados, só formato (P4).
/// </summary>
[ApiController]
[Route("api/fiscal")]
[Authorize(Policy = "tenant")]
public sealed class CadastrosFiscaisController : FiscalControllerBase
{
    private const long MaxCsvBytes = 5 * 1024 * 1024;
    private readonly IProdutoFiscalService _produtos;
    private readonly IClienteFiscalService _clientes;
    private readonly INaturezaService _naturezas;

    public CadastrosFiscaisController(
        IProdutoFiscalService produtos, IClienteFiscalService clientes, INaturezaService naturezas)
    {
        _produtos = produtos;
        _clientes = clientes;
        _naturezas = naturezas;
    }

    [HttpPut("produtos/{produtoId:guid}")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager}")]
    public async Task<IActionResult> UpsertProduto(
        Guid produtoId, [FromBody] UpsertProdutoFiscalCommand body, CancellationToken ct)
    {
        try
        {
            return Ok(await _produtos.UpsertAsync(
                (body with { ProdutoId = produtoId }).WithTenant(User.GetRequiredTenantId()), ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("produtos/{produtoId:guid}")]
    public async Task<IActionResult> GetProduto(Guid produtoId, CancellationToken ct)
    {
        var dto = await _produtos.GetAsync(User.GetRequiredTenantId(), produtoId, ct);
        return dto is null ? NotFound("Perfil fiscal do produto não encontrado.") : Ok(dto);
    }

    [HttpGet("produtos/pendencias")]
    public async Task<IActionResult> Pendencias(
        [FromQuery] Guid[] produtoId, [FromQuery] int? crt, CancellationToken ct)
    {
        if (produtoId.Length == 0 || produtoId.Length > 200)
            return BusinessRuleFailure(new BusinessRuleViolationException("Informe de 1 a 200 produtoId."));
        CrtFiscal? crtEnum = crt is null ? null : (CrtFiscal)crt.Value;
        return Ok(await _produtos.PendenciasAsync(User.GetRequiredTenantId(), produtoId, crtEnum, ct));
    }

    [HttpPost("produtos/importar")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager}")]
    [RequestSizeLimit(MaxCsvBytes)]
    public async Task<IActionResult> ImportarProdutos(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0 || arquivo.Length > MaxCsvBytes)
            return BusinessRuleFailure(new BusinessRuleViolationException("Arquivo CSV obrigatório (≤5 MB)."));
        try
        {
            await using var stream = arquivo.OpenReadStream();
            using var copia = new MemoryStream();
            await stream.CopyToAsync(copia, ct);
            copia.Position = 0;
            return Ok(await _produtos.ImportarCsvAsync(User.GetRequiredTenantId(), copia, ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("clientes/{clienteId:guid}")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager}")]
    public async Task<IActionResult> UpsertCliente(
        Guid clienteId, [FromBody] UpsertClienteFiscalCommand body, CancellationToken ct)
    {
        try
        {
            return Ok(await _clientes.UpsertAsync(
                (body with { ClienteId = clienteId }).WithTenant(User.GetRequiredTenantId()), ct));
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("clientes/{clienteId:guid}")]
    public async Task<IActionResult> GetCliente(Guid clienteId, CancellationToken ct)
    {
        var dto = await _clientes.GetAsync(User.GetRequiredTenantId(), clienteId, ct);
        return dto is null ? NotFound("Perfil fiscal do cliente não encontrado.") : Ok(dto);
    }

    [HttpGet("clientes/{clienteId:guid}/validacao")]
    public async Task<IActionResult> ValidarCliente(Guid clienteId, CancellationToken ct)
        => Ok(await _clientes.ValidarAsync(User.GetRequiredTenantId(), clienteId, ct));

    [HttpPost("naturezas")]
    [Authorize(Roles = PlatformRoles.TenantAdmin)]
    public async Task<IActionResult> CreateNatureza([FromBody] CreateNaturezaCommand body, CancellationToken ct)
    {
        try
        {
            var dto = await _naturezas.CreateAsync(body.WithTenant(User.GetRequiredTenantId()), ct);
            return CreatedAtAction(nameof(ListNaturezas), dto);
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("naturezas")]
    public async Task<IActionResult> ListNaturezas(CancellationToken ct)
        => Ok(await _naturezas.ListAsync(User.GetRequiredTenantId(), ct));

    [HttpDelete("naturezas/{id:guid}")]
    [Authorize(Roles = PlatformRoles.TenantAdmin)]
    public async Task<IActionResult> DeleteNatureza(Guid id, CancellationToken ct)
    {
        var ok = await _naturezas.DeleteAsync(User.GetRequiredTenantId(), id, ct);
        return ok ? NoContent() : NotFound("Natureza não encontrada.");
    }
}
