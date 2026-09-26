using System.Security.Claims;
using Fiscal.Application.IntegrationServices;
using Microsoft.AspNetCore.Authorization;

namespace Fiscal.Api.Auth;

/// <summary>
/// Requisito de autorização: o tenant do token tem o módulo "fiscal" ativo.
/// Consulta o Identity (/me/modules) via IModuleAccessChecker com cache.
/// Falha da consulta = fail-CLOSED (R10).
/// </summary>
public sealed class ModuleActiveRequirement(string moduleSlug) : IAuthorizationRequirement
{
    public string ModuleSlug { get; } = moduleSlug;
}

public sealed class ModuleActiveHandler(IModuleAccessChecker checker)
    : AuthorizationHandler<ModuleActiveRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModuleActiveRequirement requirement)
    {
        var tenantClaim = context.User.FindFirstValue(Fiscal.Domain.Common.JwtClaims.TenantId);
        if (tenantClaim is null || !Guid.TryParse(tenantClaim, out var tenantId))
            return; // sem claim tenant_id ⇒ não satisfeito (403, R5)

        if (await checker.IsModuleActiveAsync(tenantId, requirement.ModuleSlug))
            context.Succeed(requirement);
    }
}
