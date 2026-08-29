namespace Identity.Application.Branches;

/// <summary>Representação de API de uma filial (mesmos contratos de endereço e
/// contato usados nos commands).</summary>
public sealed record BranchDto(
    Guid Id,
    Guid TenantId,
    string Name,
    BranchAddress Address,
    BranchContact Contact,
    bool IsActive,
    DateTime? DeletedAtUtc);
