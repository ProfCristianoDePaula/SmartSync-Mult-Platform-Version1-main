using System.Security.Claims;
using Estoque.Application.IntegrationServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Auth;

/// <summary>
/// Requisito de autorização: o tenant do token tem o módulo "estoque" ativo.
/// Consulta o Identity (/me/modules) via IModuleAccessChecker com cache.
/// Falha da consulta = fail-CLOSED (decisão da Etapa 23).
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
        var tenantClaim = context.User.FindFirstValue(Estoque.Domain.Common.JwtClaims.TenantId);
        if (tenantClaim is null || !Guid.TryParse(tenantClaim, out var tenantId))
            return; // sem claim tenant_id ⇒ não satisfeito (403)

        if (await checker.IsModuleActiveAsync(tenantId, requirement.ModuleSlug))
            context.Succeed(requirement);
    }
}
