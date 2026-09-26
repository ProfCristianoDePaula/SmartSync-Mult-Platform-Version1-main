using Fiscal.Api.Extensions;
using Fiscal.Application.Emitentes;
using Fiscal.Domain.Common;
using Fiscal.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.Endpoints;

/// <summary>
/// Perfil fiscal do emitente: TenantAdmin escreve em qualquer filial do
/// próprio tenant; Manager/Seller só com concessão de configuração na filial
/// (sem ela, somente leitura). Leitura exige tenant (R5).
/// </summary>
[ApiController]
[Route("api/fiscal/emitentes")]
[Authorize(Policy = "tenant")]
public sealed class EmitentesController : FiscalControllerBase
{
    private readonly IEmitenteFiscalService _emitentes;

    public EmitentesController(IEmitenteFiscalService emitentes) => _emitentes = emitentes;

    [HttpPost]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    public async Task<IActionResult> Create([FromBody] CreateEmitenteCommand command, CancellationToken ct)
    {
        try
        {
            var dto = await _emitentes.CreateAsync(
                command.WithTenant(User.GetRequiredTenantId()),
                User.GetUserId(), UserRole(), ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex)
        {
            // P3: tenant pessoa física fora da v1 → 422 com mensagem clara.
            return ex.Code == "fiscal.emitente.cpf-bloqueado"
                ? StatusCode(StatusCodes.Status422UnprocessableEntity, new ProblemDetails
                {
                    Title = "Emitente pessoa física não suportado.",
                    Detail = ex.Message,
                    Status = StatusCodes.Status422UnprocessableEntity
                })
                : BusinessRuleFailure(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmitenteCommand body, CancellationToken ct)
    {
        try
        {
            var dto = await _emitentes.UpdateAsync(
                (body with { EmitenteId = id }).WithTenant(User.GetRequiredTenantId()),
                User.GetUserId(), UserRole(), ct);
            return dto is null ? NotFound("Emitente não encontrado.") : Ok(dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _emitentes.GetByIdAsync(User.GetRequiredTenantId(), id, ct);
        return dto is null ? NotFound("Emitente não encontrado.") : Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _emitentes.ListAsync(User.GetRequiredTenantId(), ct));

    /// <summary>Emitente da filial (usado pela integração Vendas→Fiscal).</summary>
    [HttpGet("por-filial/{branchId:guid}")]
    public async Task<IActionResult> GetByBranch(Guid branchId, CancellationToken ct)
    {
        var dto = await _emitentes.GetByBranchAsync(User.GetRequiredTenantId(), branchId, ct);
        return dto is null ? NotFound("Emitente não encontrado para a filial.") : Ok(dto);
    }

    /// <summary>Ativa/desativa a emissão automática ao finalizar venda (padrão false).</summary>
    [HttpPut("{id:guid}/emissao-automatica")]
    [Authorize(Roles = PlatformRoles.TenantAdmin)]
    public async Task<IActionResult> DefinirEmissaoAutomatica(
        Guid id, [FromBody] EmissaoAutomaticaBody body, CancellationToken ct)
    {
        try
        {
            return Ok(await _emitentes.DefinirEmissaoAutomaticaAsync(
                User.GetRequiredTenantId(), id, body.Ativa, User.GetUserId(), ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPut("{id:guid}/configuracoes")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    public async Task<IActionResult> UpsertConfiguracao(
        Guid id, [FromBody] UpsertConfiguracaoBody body, CancellationToken ct)
    {
        try
        {
            var dto = await _emitentes.UpsertConfiguracaoAsync(
                new UpsertConfiguracaoCommand(id, body.Tipo, body.Ambiente, body.Habilitado,
                    body.Serie, body.ModoIntegracao, body.ReferenciaCertificado, body.CscId)
                    .WithTenant(User.GetRequiredTenantId()),
                User.GetUserId(), UserRole(), ct);
            return Ok(dto);
        }
        catch (ValidationException ex) { return ValidationFailure(ex); }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpGet("{id:guid}/prontidao")]
    public async Task<IActionResult> Prontidao(
        Guid id, [FromQuery] int tipo, [FromQuery] int ambiente, CancellationToken ct)
    {
        try
        {
            return Ok(await _emitentes.GetProntidaoAsync(
                User.GetRequiredTenantId(), id,
                (TipoDocumentoFiscal)tipo, (AmbienteFiscal)ambiente, ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPost("{id:guid}/ambientes/{tipo:int}/promover-producao")]
    [Authorize(Roles = PlatformRoles.TenantAdmin)]
    public async Task<IActionResult> PromoverProducao(
        Guid id, int tipo, [FromBody] PromoverBody body, CancellationToken ct)
    {
        try
        {
            var dto = await _emitentes.PromoverProducaoAsync(
                new PromoverProducaoCommand(id, (TipoDocumentoFiscal)tipo, body.TextoConfirmacao)
                    .WithContext(User.GetRequiredTenantId(), User.GetUserId(), UserRole()), ct);
            return Ok(dto);
        }
        catch (BusinessRuleViolationException ex)
        {
            return ex.Code == "fiscal.producao-bloqueada"
                ? StatusCode(StatusCodes.Status409Conflict, new ProblemDetails
                {
                    Title = "Produção bloqueada.", Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict
                })
                : BusinessRuleFailure(ex);
        }
    }

    [HttpPut("{id:guid}/csc")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager}")]
    public async Task<IActionResult> DefinirCsc(
        Guid id, [FromBody] CscBody body, [FromServices] Fiscal.Application.Certificados.ICscService csc,
        CancellationToken ct)
    {
        try
        {
            await csc.DefinirTokenAsync(User.GetRequiredTenantId(), id, body.Tipo,
                body.CscId, body.Token, User.GetUserId(), ct);
            return NoContent();
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    [HttpPost("{id:guid}/testar-conexao")]
    [Authorize(Roles = $"{PlatformRoles.TenantAdmin},{PlatformRoles.Manager},{PlatformRoles.Seller}")]
    public async Task<IActionResult> TestarConexao(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _emitentes.TestarConexaoAsync(
                User.GetRequiredTenantId(), id, User.GetUserId(), UserRole(), ct));
        }
        catch (BusinessRuleViolationException ex) { return BusinessRuleFailure(ex); }
    }

    private string UserRole()
        => User.FindFirst(Fiscal.Domain.Common.JwtClaims.Role)?.Value ?? "";

    public sealed record UpsertConfiguracaoBody(
        TipoDocumentoFiscal Tipo,
        AmbienteFiscal Ambiente,
        bool Habilitado,
        string Serie,
        ModoIntegracao ModoIntegracao,
        Guid? ReferenciaCertificado,
        string? CscId);

    public sealed record PromoverBody(string TextoConfirmacao);

    public sealed record CscBody(TipoDocumentoFiscal Tipo, string CscId, string Token);

    public sealed record EmissaoAutomaticaBody(bool Ativa);
}
