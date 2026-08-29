namespace Identity.Application.Modules;

/// <summary>Soft delete de um módulo (inativa; não remove fisicamente).</summary>
public sealed record SoftDeleteModuleCommand(Guid Id);
