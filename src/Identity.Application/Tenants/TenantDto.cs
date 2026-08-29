using Identity.Domain.Enums;

namespace Identity.Application.Tenants;

/// <summary>Representação de um tenant para a API (resposta de CRUD).</summary>
public sealed record TenantDto(
    Guid Id,
    string LegalName,
    string TradeName,
    TipoPessoa TipoPessoa,
    string Documento,
    string Email,
    TenantStatus Status,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? DeletedAtUtc);
