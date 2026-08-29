namespace Identity.Application.Branches;

/// <summary>Soft delete de uma filial (inativa, não remove fisicamente).</summary>
public sealed record SoftDeleteBranchCommand(
    Guid TenantId,
    Guid BranchId);
