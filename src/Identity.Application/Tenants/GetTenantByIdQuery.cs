namespace Identity.Application.Tenants;

/// <summary>Consulta de um tenant por Id (apenas ativos, via query filter).</summary>
public sealed record GetTenantByIdQuery(Guid Id);
