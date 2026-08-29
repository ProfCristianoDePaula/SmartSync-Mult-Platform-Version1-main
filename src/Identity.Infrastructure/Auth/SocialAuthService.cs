using Identity.Application.Auth;
using Identity.Application.Uniqueness;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Login com provedores sociais (Google/Facebook) EXCLUSIVO para a role Client.
///
/// Fluxo (Etapa 05 + correção da Etapa 21):
/// - Login recorrente: localiza pelo par (provider, providerKey) em
///   AspNetUserLogins (UserManager.FindByLoginAsync) e reutiliza a conta — o
///   e-mail NÃO é re-checado.
/// - Primeiro login: cria um ApplicationUser com role Client vinculado ao
///   tenant do "state" e grava o vínculo (provider, providerKey) via
///   UserManager.AddLoginAsync, tudo na MESMA transação.
/// - Conflito real: e-mail já existe na plataforma mas nunca foi vinculado a
///   este provedor → SocialAuthError.EmailConflict (anti-takeover, Etapa 05).
/// </summary>
public sealed class SocialAuthService : ISocialAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IdentityDbContext _dbContext;
    private readonly UserUniquenessValidator _userUniqueness;

    private static readonly IReadOnlyList<string> Providers =
        ["Google", "Facebook"];

    public SocialAuthService(
        UserManager<ApplicationUser> userManager,
        TokenService tokenService,
        JwtOptions jwtOptions,
        IdentityDbContext dbContext,
        UserUniquenessValidator userUniqueness)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions;
        _dbContext = dbContext;
        _userUniqueness = userUniqueness;
    }

    public IReadOnlyList<string> SupportedProviders => Providers;

    public async Task<SocialAuthResult> LoginAsync(
        SocialLoginRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderKey) ||
            string.IsNullOrWhiteSpace(request.Email))
            return SocialAuthResult.Fail(SocialAuthError.InvalidRequest);

        var provider = NormalizeProvider(request.Provider);
        if (!Providers.Contains(provider, StringComparer.OrdinalIgnoreCase))
            return SocialAuthResult.Fail(SocialAuthError.ProviderUnsupported);

        // O usuário social é SEMPRE um Client de um tenant.
        if (request.TenantId is null)
            return SocialAuthResult.Fail(SocialAuthError.MissingTenantId);

        var tenantId = TenantId.From(request.TenantId.Value);
        var tenantExists = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
            return SocialAuthResult.Fail(SocialAuthError.TenantNotFound);

        // (a) LOGIN RECORRENTE: o par (provider, providerKey) já está vinculado
        // em AspNetUserLogins. É o mesmo usuário voltando pelo mesmo provedor —
        // emite os tokens sem re-checar e-mail (causa do bug da Etapa 21: o
        // vínculo nunca era persistido de forma atômica e o login recorrente
        // caía no conflito de "e-mail já existe").
        var user = await _userManager.FindByLoginAsync(provider, request.ProviderKey);
        if (user is not null)
            return await AuthorizeAndIssueAsync(user, tenantId, ct);

        // (b) SEM VÍNCULO: tenta criar um novo Client para esse provedor. Se o
        // e-mail já existe na plataforma (mas nunca foi vinculado a este
        // provedor), é um conflito real anti-takeover → erro tipado.
        var created = await CreateSocialClientAsync(request, provider, tenantId, ct);
        if (created.Error is not null)
            return SocialAuthResult.Fail(created.Error.Value);
        if (created.User is null)
            return SocialAuthResult.Fail(SocialAuthError.EmailConflict);

        // (c) Primeiro login concluído: segue o mesmo caminho de autorização.
        return await AuthorizeAndIssueAsync(created.User, tenantId, ct);
    }

    private async Task<SocialAuthResult> AuthorizeAndIssueAsync(
        ApplicationUser user,
        TenantId tenantId,
        CancellationToken ct)
    {
        // Role: o login social é exclusivo para Client (Etapa 05).
        if (!await _userManager.IsInRoleAsync(user, Roles.Client))
            return SocialAuthResult.Fail(SocialAuthError.NotClient);

        // O Client pertence a um tenant; o tenant do "state" precisa ser o dele.
        if (user.TenantId != tenantId)
            return SocialAuthResult.Fail(SocialAuthError.TenantMismatch);

        var refreshPair = await _tokenService.IssueRefreshTokenAsync(user.Id, DateTime.UtcNow, ct);
        var roles = await _userManager.GetRolesAsync(user);
        var access = await _tokenService.CreateAccessTokenAsync(user, roles, ct);

        return SocialAuthResult.Ok(new TokenResponse(
            access,
            _jwtOptions.AccessTokenLifetimeMinutes * 60,
            refreshPair.Value,
            _jwtOptions.RefreshTokenLifetimeDays * 24 * 60 * 60,
            !user.ProfileComplete));
    }

    /// <summary>
    /// Cria o Client do primeiro login social e grava o vínculo
    /// (provider, providerKey) em AspNetUserLogins — o passo que faltava para o
    /// login recorrente reencontrar a conta. A criação é ATÔMICA (usuário +
    /// role + vínculo na mesma transação): uma falha parcial não deixa usuário
    /// órfão sem vínculo (que condenaria o retorno à 401 de "e-mail já existe").
    /// </summary>
    private async Task<(ApplicationUser? User, SocialAuthError? Error)> CreateSocialClientAsync(
        SocialLoginRequest request,
        string provider,
        TenantId tenantId,
        CancellationToken ct)
    {
        var normalized = _userManager.NormalizeEmail(request.Email);

        // Anti-takeover: e-mail já existe em QUALQUER tenant da plataforma e
        // nunca foi vinculado a este provedor → conflito real.
        var emailExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.NormalizedEmail == normalized, ct);
        if (emailExists)
            return (null, SocialAuthError.EmailConflict);

        // Unicidade por tenant (Etapa 04) antes de criar.
        try
        {
            await _userUniqueness.EnsureEmailUniqueAsync(tenantId, request.Email, null, ct);
        }
        catch (BusinessRuleViolationException)
        {
            return (null, SocialAuthError.EmailConflict);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Email : request.FullName,
            TenantId = tenantId,
            EmailConfirmed = true, // e-mail veio validado pelo provedor externo
            LockoutEnabled = true
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        var created = await _userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return (null, SocialAuthError.CreateFailed);
        }

        var role = await _userManager.AddToRoleAsync(user, Roles.Client);
        if (!role.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return (null, SocialAuthError.CreateFailed);
        }

        var linked = await _userManager.AddLoginAsync(
            user, new UserLoginInfo(provider, request.ProviderKey, provider));
        if (!linked.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return (null, SocialAuthError.CreateFailed);
        }

        await tx.CommitAsync(ct);
        return (user, null);
    }

    private static string NormalizeProvider(string provider)
        => provider.Trim().ToLowerInvariant() switch
        {
            "google" => "Google",
            "facebook" => "Facebook",
            _ => provider.Trim()
        };
}
