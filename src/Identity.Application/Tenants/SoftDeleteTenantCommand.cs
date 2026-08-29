namespace Identity.Application.Tenants;

/// <summary>Soft delete de um tenant (inativa; não remove fisicamente).</summary>
public sealed record SoftDeleteTenantCommand(Guid Id);
