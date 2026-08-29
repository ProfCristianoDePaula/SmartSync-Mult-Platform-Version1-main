using Identity.Application.RoleCatalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// Catálogo de roles da plataforma. Autenticado (qualquer role): a lista é
/// consumida por UIs de admin/gestão para exibir os papéis disponíveis.
/// </summary>
[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    /// <summary>Lista todas as roles da plataforma, na ordem canônica.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _roleService.GetAllAsync(ct));
}
