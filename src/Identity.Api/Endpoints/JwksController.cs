using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Endpoints;

[ApiController]
[Route("api/auth")]
public sealed class JwksController : ControllerBase
{
    private const string JwksCacheKey = "jwks:public-key-set";
    private static readonly TimeSpan JwksCacheLifetime = TimeSpan.FromHours(5);

    private readonly SigningKeyProvider _signingKeyProvider;
    private readonly IMemoryCache _cache;

    public JwksController(SigningKeyProvider signingKeyProvider, IMemoryCache cache)
    {
        _signingKeyProvider = signingKeyProvider;
        _cache = cache;
    }

    /// <summary>
    /// Exposição da chave pública (JWKS) para que os demais microsserviços
    /// validem os access tokens emitidos por este serviço.
    /// Cacheado (Etapa 07) para não reler o PEM da chave a cada requisição.
    /// </summary>
    [HttpGet("jwks")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(JsonWebKeySet), StatusCodes.Status200OK)]
    public IActionResult GetJwks()
        => Ok(_cache.GetOrCreate(JwksCacheKey, entry =>
        {
            entry.SlidingExpiration = JwksCacheLifetime;
            // Chave pública é eficazmente imutável enquanto durar o processo;
            // o cache é invalidado toda vez que a app reinicia.
            return new JsonWebKeySet { Keys = { _signingKeyProvider.PublicKey } };
        }));
}