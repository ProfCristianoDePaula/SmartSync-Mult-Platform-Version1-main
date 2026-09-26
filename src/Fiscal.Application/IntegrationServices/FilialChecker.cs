using Fiscal.Domain.Common;

namespace Fiscal.Application.IntegrationServices;

/// <summary>
/// Resultado da validação de posse de filial.
/// Allowed = pertence ao tenant; Denied = não pertence/inexistente;
/// Unknown = não foi possível validar — escrita fiscal é fail-closed.
/// </summary>
public enum FilialAccess
{
    Allowed,
    Denied,
    Unknown
}

/// <summary>
/// Valida que uma filial pertence ao tenant via API HTTP do Identity.
/// </summary>
public interface IFilialAccessChecker
{
    Task<FilialAccess> ValidateAsync(TenantId tenantId, Guid branchId, CancellationToken ct = default);
}
