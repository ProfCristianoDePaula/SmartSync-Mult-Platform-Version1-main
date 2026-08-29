using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Uniqueness;

/// <summary>
/// Regras de unicidade do agregado Tenant: documento (CPF/CNPJ) e e-mail
/// GLOBAIS (sem escopo de tenant — um cliente nunca pode se repetir na
/// plataforma). A unicidade do documento vale para o número, independente do
/// tipo de pessoa (Etapa 17).
/// </summary>
public sealed class TenantUniquenessValidator
{
    private readonly IUniquenessChecker _checker;

    public TenantUniquenessValidator(IUniquenessChecker checker) => _checker = checker;

    public async Task EnsureDocumentoUniqueAsync(
        Documento documento,
        TenantId? excludeTenantId = null,
        CancellationToken ct = default)
    {
        if (await _checker.IsTenantDocumentTakenAsync(documento, excludeTenantId, ct))
            throw new BusinessRuleViolationException(
                documento.Tipo == TipoPessoa.Fisica
                    ? "Já existe um tenant com este CPF."
                    : "Já existe um tenant com este CNPJ.",
                "tenant.document.duplicate");
    }

    public async Task EnsureEmailUniqueAsync(
        Email email,
        TenantId? excludeTenantId = null,
        CancellationToken ct = default)
    {
        if (await _checker.IsTenantEmailTakenAsync(email, excludeTenantId, ct))
            throw new BusinessRuleViolationException(
                "Já existe um tenant com este e-mail.", "tenant.email.duplicate");
    }

    public async Task ValidateAsync(
        Documento documento,
        Email email,
        TenantId? excludeTenantId = null,
        CancellationToken ct = default)
    {
        await EnsureDocumentoUniqueAsync(documento, excludeTenantId, ct);
        await EnsureEmailUniqueAsync(email, excludeTenantId, ct);
    }
}