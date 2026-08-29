using System.Security.Claims;
using Identity.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Endpoints;

/// <summary>
/// Login social (Google/Facebook) — exclusivo para a role Client.
///
/// GET /api/auth/external-login/{provider}?tenantId=...
///   Inicia o fluxo OAuth. O tenant não pode ser descoberto pelo Google/Facebook,
///   então ele vai dentro do "state" (AuthenticationProperties.Items), que o
///   OAuth handler serializa e devolve de volta no callback.
///
/// GET /api/auth/external-login-callback
///   Recebe o ticket autenticado do provider, relê o tenantId do state e
///   delega ao ISocialAuthService (cria o Client no 1º login; vincula nos
///   demais), emitindo o JWT da plataforma. Os erros são diferenciados
///   (Etapa 21): 400 para request/tenant inválido, 401 para conflito real de
///   e-mail (anti-takeover) e 403 para conta sem a role Client.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class ExternalAuthController : ControllerBase
{
    private readonly ISocialAuthService _socialAuthService;
    private readonly ILogger<ExternalAuthController> _logger;

    public ExternalAuthController(ISocialAuthService socialAuthService, ILogger<ExternalAuthController> logger)
    {
        _socialAuthService = socialAuthService;
        _logger = logger;
    }

    [HttpGet("external-login/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ExternalLogin(
        [FromRoute] string provider,
        [FromQuery] Guid? tenantId)
    {
        if (!_socialAuthService.SupportedProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { title = $"Provedor social '{provider}' não suportado." });

        // O scheme registrado tem o nome canônico ("Google"/"Facebook"). O valor
        // da rota pode vir em qualquer caixa; passar a minúscula ao Challenge
        // lançaria "No authentication handler is registered for the scheme".
        var scheme = NormalizeScheme(provider);

        if (tenantId is null)
            return BadRequest(new { error = "O parâmetro 'tenantId' é obrigatório no login social." });

        // Callback para onde o provider redireciona depois de autenticar.
        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/auth/external-login-callback";

        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl,
            Items =
            {
                ["tenantId"] = tenantId.Value.ToString(),
                ["provider"] = scheme
            }
        };

        return Challenge(properties, scheme);
    }

    [HttpGet("external-login-callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExternalLoginCallback(CancellationToken ct)
    {
        // Lê o ticket que o provider depositou no cookie externo do Identity.
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!result.Succeeded || result.Principal is null)
            return Unauthorized(new { error = "Falha ao autenticar via provedor externo." });

        var provider = result.Properties?.Items["provider"] ?? string.Empty;
        var tenantRaw = result.Properties?.Items["tenantId"];
        if (string.IsNullOrWhiteSpace(provider) ||
            !Guid.TryParse(tenantRaw, out var tenantId))
        {
            _logger.LogWarning(
                "Callback social sem state válido: providerVazio={ProviderEmpty}, tenantIdValido={TenantIdValido}",
                string.IsNullOrWhiteSpace(provider), Guid.TryParse(tenantRaw, out _));
            return BadRequest(new { error = "State do fluxo social inválido (tenantId/provider ausentes)." });
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name);
        var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(providerKey))
        {
            _logger.LogWarning(
                "Callback social sem e-mail/sub do IdP: emailPresent={EmailPresent}, providerKeyPresent={ProviderKeyPresent}",
                !string.IsNullOrWhiteSpace(email), !string.IsNullOrWhiteSpace(providerKey));
            return BadRequest(new { error = "Provider não devolveu e-mail/sub identificadores." });
        }

        // Encerra o "fôleto" intermediário (cookie externo) antes de emitir o JWT.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var request = new SocialLoginRequest(
            provider,
            providerKey,
            email,
            name,
            tenantId);

        var socialResult = await _socialAuthService.LoginAsync(request, ct);
        if (socialResult.Token is not null)
        {
            _logger.LogInformation(
                "Login social concluído: provider={Provider}, tenantId={TenantId}, emailPresent={EmailPresent}, resultado={Result}",
                provider, tenantId, email is not null, "success");
            return Ok(socialResult.Token);
        }

        _logger.LogWarning(
            "Login social recusado: provider={Provider}, tenantId={TenantId}, emailPresent={EmailPresent}, resultado={Result}",
            provider, tenantId, email is not null, socialResult.Error);
        return MapSocialError(socialResult.Error);
    }

    private IActionResult MapSocialError(SocialAuthError? error)
    {
        switch (error)
        {
            case SocialAuthError.InvalidRequest:
                return BadRequest(new { error = "Provider não devolveu e-mail/sub identificadores." });
            case SocialAuthError.ProviderUnsupported:
                return BadRequest(new { error = "Provedor social não suportado." });
            case SocialAuthError.MissingTenantId:
                return BadRequest(new { error = "O parâmetro 'tenantId' é obrigatório no login social." });
            case SocialAuthError.TenantNotFound:
                return BadRequest(new { error = "Tenant não encontrado." });
            case SocialAuthError.CreateFailed:
                return BadRequest(new { error = "Não foi possível criar a conta vinculada ao provedor." });
            case SocialAuthError.EmailConflict:
                return Unauthorized(new { error = "Já existe uma conta com este e-mail cadastrada de outra forma." });
            case SocialAuthError.TenantMismatch:
                return Unauthorized(new { error = "A conta vinculada ao provedor pertence a outro tenant." });
            case SocialAuthError.NotClient:
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "A conta vinculada ao provedor não possui a role Client."
                });
            default:
                return Unauthorized(new { error = "Não foi possível completar o login social." });
        }
    }

    private static string NormalizeScheme(string provider)
        => provider.Trim().ToLowerInvariant() switch
        {
            "google" => "Google",
            "facebook" => "Facebook",
            _ => provider.Trim()
        };
}
