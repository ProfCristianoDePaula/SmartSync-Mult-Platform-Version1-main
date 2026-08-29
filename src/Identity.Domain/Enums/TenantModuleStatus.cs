namespace Identity.Domain.Enums;

/// <summary>
/// Situação do vínculo de um Tenant com um Module (Etapa 15). Active = vigente
/// (EndDateUtc nulo); Inactive = encerrado (EndDateUtc preenchido), preservando
/// histórico para billing.
/// </summary>
public enum TenantModuleStatus
{
    Active = 1,
    Inactive = 2
}
