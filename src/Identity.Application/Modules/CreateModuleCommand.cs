namespace Identity.Application.Modules;

/// <summary>Dados para cadastrar um novo módulo. O Slug é imutável após a criação.</summary>
public sealed record CreateModuleCommand(
    string Name,
    string Slug,
    string? Description);
