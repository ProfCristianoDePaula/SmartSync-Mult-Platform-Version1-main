using Identity.Domain.Common;

namespace Identity.Application.Uniqueness;

/// <summary>
/// Regras de unicidade de usuário: semântica depende da role.
/// - Manager, Seller, Delivery, Client: escopo POR TENANT
///   (mesmo e-mail/CPF pode existir em tenants diferentes).
/// - SuperAdmin/TenantAdmin (TenantId nulo): escopo GLOBAL.
/// A validação roda ANTES de persistir; a constraint do banco é a última linha
/// de defesa (defesa em profundidade).
/// </summary>
public sealed class UserUniquenessValidator
{
    private readonly IUniquenessChecker _checker;

    public UserUniquenessValidator(IUniquenessChecker checker) => _checker = checker;

    public async Task EnsureEmailUniqueAsync(
        TenantId? tenantId,
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default)
    {
        if (await _checker.IsUserEmailTakenAsync(tenantId, email, excludeUserId, ct))
            throw new BusinessRuleViolationException(
                tenantId is null
                    ? "Já existe uma conta global com este e-mail."
                    : "Já existe um usuário deste tenant com este e-mail.",
                "user.email.duplicate");
    }

    public async Task EnsureDocumentUniqueAsync(
        TenantId? tenantId,
        string document,
        Guid? excludeUserId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(document))
            return;

        if (await _checker.IsUserDocumentTakenAsync(tenantId, document, excludeUserId, ct))
            throw new BusinessRuleViolationException(
                tenantId is null
                    ? "Já existe uma conta global com este documento."
                    : "Já existe um usuário deste tenant com este documento.",
                "user.document.duplicate");
    }
}