namespace Identity.Application.Plans;

/// <summary>
/// Limites efetivos de UM tenant, derivados dos planos dos vínculos ativos
/// (tenant_modules → plans). <c>null</c> = "sem limite" (convenção da
/// plataforma — Etapa 18).
/// </summary>
public sealed record PlanLimits(int? MaxBranches, int? MaxUsers, int? MaxStorageMb);
