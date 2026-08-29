namespace Identity.Application.Plans;

/// <summary>Soft delete de um plano de um módulo (inativa; não remove fisicamente).</summary>
public sealed record SoftDeletePlanCommand(
    Guid ModuleId,
    Guid Id);
