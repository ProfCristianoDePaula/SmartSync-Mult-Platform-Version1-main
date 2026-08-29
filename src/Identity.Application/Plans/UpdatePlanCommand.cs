namespace Identity.Application.Plans;

/// <summary>
/// Dados para editar um plano existente (o ModuleId e o Id do plano vêm da
/// rota). Um plano não troca de módulo — a unicidade de nome é por módulo.
/// Limites <c>null</c> significam "sem limite" (convenção da plataforma).
/// </summary>
public sealed record UpdatePlanCommand(
    Guid ModuleId,
    Guid Id,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int TrialDays,
    IReadOnlyList<string> Features,
    int? MaxBranches,
    int? MaxUsers,
    int? MaxStorageMb);
