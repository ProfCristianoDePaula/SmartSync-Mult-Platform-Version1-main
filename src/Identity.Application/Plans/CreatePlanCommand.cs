namespace Identity.Application.Plans;

/// <summary>
/// Dados para cadastrar um novo plano de assinatura DENTRO de um módulo
/// (o ModuleId vem da rota <c>api/modules/{moduleId}/plans</c>). Limites
/// <c>null</c> significam "sem limite" (convenção da plataforma).
/// </summary>
public sealed record CreatePlanCommand(
    Guid ModuleId,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int TrialDays,
    IReadOnlyList<string> Features,
    int? MaxBranches,
    int? MaxUsers,
    int? MaxStorageMb);
