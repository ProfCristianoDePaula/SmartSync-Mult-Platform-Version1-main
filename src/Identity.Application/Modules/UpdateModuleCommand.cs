namespace Identity.Application.Modules;

/// <summary>
/// Dados para editar um módulo existente (o Id vem da rota). O Slug é imutável
/// (identidade programática do módulo) — apenas nome e descrição são editáveis.
/// </summary>
public sealed record UpdateModuleCommand(
    Guid Id,
    string Name,
    string? Description);
