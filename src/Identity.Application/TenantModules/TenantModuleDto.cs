using Identity.Domain.Enums;

namespace Identity.Application.TenantModules;

/// <summary>
/// Representação de um vínculo tenant↔módulo para a API. O módulo é resolvido
/// (nome) para exibição no backend administrativo; o plano é referenciado
/// apenas pelo Id (FK) — o nome é obtido na tabela de planos quando necessário.
/// </summary>
public sealed record TenantModuleDto(
    Guid Id,
    Guid TenantId,
    Guid ModuleId,
    string ModuleName,
    Guid PlanId,
    TenantModuleStatus Status,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime CreatedAtUtc);
