using System.Security.Cryptography;
using System.Text;
using Identity.Application.Auth;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Infrastructure.Security;

/// <summary>
/// Emissão de access tokens (JWT RS256) e gestão de refresh tokens com rotação:
/// cada uso do refresh gera um novo e revoga o anterior.
/// </summary>
public sealed class TokenService
{
    private readonly SigningKeyProvider _signingKeyProvider;
    private readonly JwtOptions _jwtOptions;
    private readonly IdentityDbContext _dbContext;

    public TokenService(
        SigningKeyProvider signingKeyProvider,
        JwtOptions jwtOptions,
        IdentityDbContext dbContext)
    {
        _signingKeyProvider = signingKeyProvider;
        _jwtOptions = jwtOptions;
        _dbContext = dbContext;
    }

    public async Task<string> CreateAccessTokenAsync(
        ApplicationUser user,
        IList<string> roles,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            [JwtClaims.UserId] = user.Id.ToString(),
            [JwtClaims.FullName] = user.FullName,
            [JwtClaims.Email] = user.Email ?? string.Empty,
            [JwtClaims.EmailConfirmed] = user.EmailConfirmed ? "true" : "false",
            [JwtClaims.PhoneConfirmed] = user.PhoneNumberConfirmed ? "true" : "false",
            [JwtClaims.ProfileComplete] = user.ProfileComplete ? "true" : "false",
        };

        if (user.TenantId is not null)
            claims[JwtClaims.TenantId] = user.TenantId.Value.Value.ToString();

        claims[JwtClaims.Role] = roles;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes),
            Claims = claims,
            SigningCredentials = _signingKeyProvider.SigningCredentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(descriptor);
    }

    /// <summary>Gera um refresh token (valor aleatório) e persiste o hash.</summary>
    public async Task<RefreshTokenPair> IssueRefreshTokenAsync(
        Guid userId,
        DateTime now,
        CancellationToken ct = default)
    {
        var value = GenerateRefreshTokenValue();
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(value),
            ExpiresAtUtc = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedAtUtc = now
        };

        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync(ct);
        return new RefreshTokenPair(token, value);
    }

    /// <summary>
    /// Valida um refresh token retornando-o com seu valor em claro original.
    /// Retorna null se inexistente, revogado ou expirado.
    /// </summary>
    public async Task<RefreshTokenPair?> ValidateRefreshTokenAsync(
        string value,
        CancellationToken ct = default)
    {
        var hash = Hash(value);
        var token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (token is null || token.RevokedAtUtc is not null || token.ExpiresAtUtc <= DateTime.UtcNow)
            return null;

        return new RefreshTokenPair(token, value);
    }

    /// <summary>Revoga um refresh token específico (logout de uma sessão).</summary>
    public async Task RevokeRefreshTokenAsync(RefreshTokenPair pair, CancellationToken ct = default)
    {
        var entity = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.Id == pair.Token.Id, ct);
        if (entity is null || entity.RevokedAtUtc is not null)
            return;

        entity.RevokedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>Revoga TODOS os refresh tokens ativos do usuário (logout-all).</summary>
    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var active = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(ct);

        if (active.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var token in active)
            token.RevokedAtUtc = now;

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Revoga TODOS os refresh tokens ativos dos usuários vinculados a um tenant
    /// (soft delete do tenant — pendência da Etapa 13). Garante que, mesmo que o
    /// tenant volte a existir, sessões antigas não são recuperáveis.
    /// </summary>
    public async Task RevokeAllTenantRefreshTokensAsync(Guid tenantId, CancellationToken ct = default)
    {
        var active = await (
            from rt in _dbContext.RefreshTokens
            join u in _dbContext.Users on rt.UserId equals u.Id
            where u.TenantId == TenantId.From(tenantId)
                  && rt.RevokedAtUtc == null
                  && rt.ExpiresAtUtc > DateTime.UtcNow
            select rt)
            .ToListAsync(ct);

        if (active.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var token in active)
            token.RevokedAtUtc = now;

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>Revoga o token antigo marcando-o como substituído por um novo hash.</summary>
    public async Task RotateRefreshTokenAsync(
        RefreshTokenPair oldPair,
        string newValue,
        CancellationToken ct = default)
    {
        var entity = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.Id == oldPair.Token.Id, ct);
        if (entity is null || entity.RevokedAtUtc is not null)
            return;

        entity.RevokedAtUtc = DateTime.UtcNow;
        entity.ReplacedByTokenHash = Hash(newValue);
        await _dbContext.SaveChangesAsync(ct);
    }

    private static string GenerateRefreshTokenValue()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}

/// <summary>Par (entidade persistida + valor em claro) usado no fluxo de rotação.</summary>
public sealed record RefreshTokenPair(RefreshToken Token, string Value);