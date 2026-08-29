namespace Identity.Application.RoleCatalog;

/// <summary>
/// Contrato do catálogo de roles da plataforma, implementado na Infrastructure.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Lista todas as roles da plataforma, na ordem canônica definida em
    /// <see cref="Identity.Domain.Common.Roles"/>; roles adicionais criadas no
    /// banco (ex.: por futuros endpoints de admin) aparecem depois.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken ct = default);
}
