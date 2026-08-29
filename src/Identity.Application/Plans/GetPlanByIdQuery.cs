namespace Identity.Application.Plans;

/// <summary>Consulta de um plano por Id dentro de um módulo (apenas planos ativos).</summary>
public sealed record GetPlanByIdQuery(
    Guid ModuleId,
    Guid Id);
