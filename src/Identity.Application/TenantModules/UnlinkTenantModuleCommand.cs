namespace Identity.Application.TenantModules;

/// <summary>
/// Encerra o vínculo ativo do tenant com o módulo (Etapa 15): inativa a
/// vigência atual registrando EndDateUtc — não remove o histórico (billing).
/// </summary>
public sealed record UnlinkTenantModuleCommand(
    Guid TenantId,
    Guid ModuleId);
