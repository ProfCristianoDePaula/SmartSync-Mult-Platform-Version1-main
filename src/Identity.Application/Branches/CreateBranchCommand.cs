namespace Identity.Application.Branches;

/// <summary>Cadastra uma filial. O <see cref="TenantId"/> vem da rota
/// (<c>/api/tenants/{tenantId}/branches</c>) e prevalece sobre o body.</summary>
public sealed record CreateBranchCommand(
    Guid TenantId,
    string Name,
    BranchAddress Address,
    BranchContact Contact);
