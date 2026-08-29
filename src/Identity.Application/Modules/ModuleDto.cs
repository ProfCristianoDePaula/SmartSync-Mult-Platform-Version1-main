namespace Identity.Application.Modules;

/// <summary>Representação de um módulo para a API (resposta de CRUD).</summary>
public sealed record ModuleDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? DeletedAtUtc);
