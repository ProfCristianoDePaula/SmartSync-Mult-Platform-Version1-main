using Identity.Domain.Common;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Uniqueness;

/// <summary>
/// Consulta de unicidade no repositório. Implementado na Infrastructure
/// (usa o IdentityDbContext diretamente). A camada de Application usa esta
/// interface para validar duplicidade ANTES de persistir — não depende apenas
/// das constraints do banco.
/// </summary>
public interface IUniquenessChecker
{
    /// <summary>True se já existe outro tenant com o mesmo documento (CPF/CNPJ) — ignora <paramref name="excludeTenantId"/>.</summary>
    Task<bool> IsTenantDocumentTakenAsync(Documento documento, TenantId? excludeTenantId = null, CancellationToken ct = default);

    /// <summary>True se já existe outro tenant com o mesmo e-mail (ignora <paramref name="excludeTenantId"/>).</summary>
    Task<bool> IsTenantEmailTakenAsync(Email email, TenantId? excludeTenantId = null, CancellationToken ct = default);

    /// <summary>
    /// True se já existe usuário com o mesmo e-mail no escopo do tenant.
    /// Quando <paramref name="tenantId"/> é nulo (SuperAdmin/TenantAdmin globais),
    /// o escopo é global (e-mail único em toda a plataforma).
    /// </summary>
    Task<bool> IsUserEmailTakenAsync(TenantId? tenantId, string email, Guid? excludeUserId = null, CancellationToken ct = default);

    /// <summary>
    /// True se já existe usuário com o mesmo documento (CPF/CNPJ) no escopo do tenant.
    /// Quando <paramref name="tenantId"/> é nulo, o escopo é global.
    /// </summary>
    Task<bool> IsUserDocumentTakenAsync(TenantId? tenantId, string document, Guid? excludeUserId = null, CancellationToken ct = default);
}