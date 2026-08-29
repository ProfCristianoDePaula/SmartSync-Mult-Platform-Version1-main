using Identity.Domain.Enums;

namespace Identity.Application.Tenants;

/// <summary>
/// Dados para editar um tenant existente (o Id vem da rota). O documento
/// (CPF/CNPJ) é imutável (identidade do tenant) e o status controla o ciclo de
/// vida (Active/Inactive/Suspended). Módulos/planos do tenant NÃO são editados
/// aqui (Etapa 15): usam os endpoints dedicados de vínculo.
/// </summary>
public sealed record UpdateTenantCommand(
    Guid Id,
    string LegalName,
    string TradeName,
    string Email,
    TenantStatus Status);
