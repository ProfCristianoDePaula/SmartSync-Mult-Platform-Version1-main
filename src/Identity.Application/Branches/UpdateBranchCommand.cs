namespace Identity.Application.Branches;

/// <summary>Edita uma filial existente. <see cref="TenantId"/> e
/// <see cref="BranchId"/> vêm da rota e prevalecem sobre o body.</summary>
public sealed record UpdateBranchCommand(
    Guid TenantId,
    Guid BranchId,
    string Name,
    BranchAddress Address,
    BranchContact Contact);
