namespace Identity.Application.Branches;

/// <summary>Detalhe de uma filial específica, sempre escopado ao tenant.</summary>
public sealed record GetBranchByIdQuery(
    Guid TenantId,
    Guid BranchId);
