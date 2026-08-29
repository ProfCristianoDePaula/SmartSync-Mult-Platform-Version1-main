namespace Identity.Application.Plans;

/// <summary>Representação de um plano para a API (resposta de CRUD). Limites
/// <c>null</c> significam "sem limite" (convenção da plataforma).</summary>
public sealed record PlanDto(
    Guid Id,
    Guid ModuleId,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int TrialDays,
    IReadOnlyList<string> Features,
    int? MaxBranches,
    int? MaxUsers,
    int? MaxStorageMb,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? DeletedAtUtc);
