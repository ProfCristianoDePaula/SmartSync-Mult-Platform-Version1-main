using System.Security.Claims;
using Estoque.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Extensions;

/// <summary>
/// Extração das claims de identidade do JWT emitido pelo Identity.
/// Padrão auditado: controllers extraem as claims e injetam nos commands —
/// os serviços nunca leem HttpContext.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>user_id da claim (obrigatório em rotas autenticadas).</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(JwtClaims.UserId), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token sem claim user_id.");

    /// <summary>tenant_id da claim (nulo para SuperAdmin global).</summary>
    public static TenantId? GetTenantIdOrNull(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(JwtClaims.TenantId), out var tenantId)
            ? TenantId.From(tenantId)
            : null;

    /// <summary>tenant_id obrigatório — rotas de negócio do Estoque exigem tenant.</summary>
    public static TenantId GetRequiredTenantId(this ClaimsPrincipal user)
        => user.GetTenantIdOrNull()
           ?? throw new UnauthorizedAccessException("Usuário sem tenant vinculado.");
}
