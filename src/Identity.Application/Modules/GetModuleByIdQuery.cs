namespace Identity.Application.Modules;

/// <summary>Consulta de um módulo por Id (apenas módulos ativos, via query filter).</summary>
public sealed record GetModuleByIdQuery(Guid Id);
