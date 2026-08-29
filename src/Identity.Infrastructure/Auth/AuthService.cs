using System.Net;
using System.Security.Cryptography;
using Identity.Application.Auth;
using Identity.Application.Notifications;
using Identity.Application.Uniqueness;
using Identity.Domain.Common;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Identity;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IdentityDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly EmailOptions _emailOptions;
    private readonly UserUniquenessValidator _userUniqueness;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        TokenService tokenService,
        JwtOptions jwtOptions,
        IdentityDbContext dbContext,
        IEmailSender emailSender,
        ISmsSender smsSender,
        IOptions<EmailOptions> emailOptions,
        UserUniquenessValidator userUniqueness,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions;
        _dbContext = dbContext;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _emailOptions = emailOptions.Value;
        _userUniqueness = userUniqueness;
        _logger = logger;
    }

    public async Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await FindByIdentifierAsync(request.Identifier, ct);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return null;

        if (!await _userManager.IsEmailConfirmedAsync(user))
            return null;

        // Etapa 13: usuário de um tenant soft-deletado não pode mais autenticar.
        // O query filter "Active" oculta tenants inativados das consultas.
        if (!await IsTenantUsableForAuthAsync(user, ct))
            return null;

        return await IssueTokensAsync(user, ct);
    }

    public async Task<TokenResponse?> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var oldPair = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken, ct);
        if (oldPair is null)
            return null;

        var user = await _userManager.FindByIdAsync(oldPair.Token.UserId.ToString());
        if (user is null)
            return null;

        // Etapa 13: o refresh também é bloqueado quando o tenant foi soft-deletado.
        if (!await IsTenantUsableForAuthAsync(user, ct))
            return null;

        // Rotação: emitir novo antes de revogar o antigo (atomização simples).
        var newPair = await _tokenService.IssueRefreshTokenAsync(user.Id, DateTime.UtcNow, ct);
        await _tokenService.RotateRefreshTokenAsync(oldPair, newPair.Value, ct);

        var access = await CreateAccessTokenAsync(user, ct);

        return new TokenResponse(
            access,
            _jwtOptions.AccessTokenLifetimeMinutes * 60,
            newPair.Value,
            _jwtOptions.RefreshTokenLifetimeDays * 24 * 60 * 60,
            !user.ProfileComplete);
    }

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
            return RegisterResult.Fail("Nome, e-mail e senha são obrigatórios.");

        var tenantId = TenantId.From(request.TenantId);
        var tenantExists = await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists)
            return RegisterResult.Fail("Tenant não encontrado.");

        // Nota: o limite de usuários do plano (MaxUsers) NÃO se aplica ao
        // cadastro público — Clients (autosserviço) não contam para o limite
        // (decisão do usuário); MaxUsers vale apenas para usuários internos
        // (TenantAdmin/Manager/Seller/Delivery), quando houver o endpoint de
        // criação. Unicidade por tenant (Etapa 04): e-mail e documento são
        // únicos no tenant.
        try
        {
            await _userUniqueness.EnsureEmailUniqueAsync(tenantId, request.Email, null, ct);
            await _userUniqueness.EnsureDocumentUniqueAsync(tenantId, request.Document ?? string.Empty, null, ct);
        }
        catch (BusinessRuleViolationException ex)
        {
            return RegisterResult.Fail(ex.Message);
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName.Trim(),
            TenantId = tenantId,
            Document = string.IsNullOrWhiteSpace(request.Document)
                ? null
                : new string(request.Document.Where(char.IsDigit).ToArray()),
            EmailConfirmed = false, // exige confirmação por e-mail (Etapa 06)
            LockoutEnabled = true
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return RegisterResult.Fail(
                created.Errors.Select(e => e.Description).ToArray());

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Client);
        if (!roleResult.Succeeded)
            return RegisterResult.Fail(
                roleResult.Errors.Select(e => e.Description).ToArray());

        // Envia o link de confirmação (reusa o fluxo da Etapa 06).
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await SendConfirmationEmailAsync(user, token, ct);

        return RegisterResult.Ok();
    }

    private async Task<TokenResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        var refreshPair = await _tokenService.IssueRefreshTokenAsync(user.Id, DateTime.UtcNow, ct);
        var access = await CreateAccessTokenAsync(user, ct);

        return new TokenResponse(
            access,
            _jwtOptions.AccessTokenLifetimeMinutes * 60,
            refreshPair.Value,
            _jwtOptions.RefreshTokenLifetimeDays * 24 * 60 * 60,
            !user.ProfileComplete);
    }

    private async Task<string> CreateAccessTokenAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return await _tokenService.CreateAccessTokenAsync(user, roles, ct);
    }

    private async Task<ApplicationUser?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var byEmail = await _userManager.FindByEmailAsync(identifier);
        if (byEmail is not null)
            return byEmail;

        var normalized = _userManager.NormalizeName(identifier);
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
    }

    /// <summary>
    /// Um usuário vinculado a um tenant só autentica se o tenant existir e
    /// estiver ativo (não soft-deletado). Usuários globais (SuperAdmin/
    /// TenantAdmin, TenantId nulo) não passam por esta verificação.
    /// </summary>
    private async Task<bool> IsTenantUsableForAuthAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.TenantId is null)
            return true;

        // O query filter "Active" do Tenant oculta soft-deletados: não encontrar
        // significa que o tenant foi inativado (ou não existe).
        return await _dbContext.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == user.TenantId, ct);
    }

    public async Task ResendConfirmationEmailAsync(
        string email,
        CancellationToken ct = default)
    {
        // Resposta sempre "ok": não revela se o e-mail existe na plataforma.
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed)
        {
            _logger.LogInformation(
                "Reenvio de confirmação ignorado para {Email}: {Motivo}.",
                email,
                user is null ? "e-mail não cadastrado" : "conta já confirmada");
            return;
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await SendConfirmationEmailAsync(user, token, ct);
    }

    public async Task<bool> ConfirmEmailAsync(
        string email,
        string token,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return false;

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task SendSmsCodeAsync(
        Guid userId,
        string phoneNumber,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return;

        // Código numérico curto (6 dígitos), mesmo padrão do reset por SMS:
        // apenas o hash é persistido; o valor viaja somente no SMS.
        var code = GenerateNumericCode(6);

        // Invalida códigos anteriores não usados do mesmo usuário antes de
        // persistir o novo (sempre o mais recente vale).
        var outstanding = await _dbContext.PhoneVerificationCodes
            .Where(x => x.UserId == user.Id && x.UsedAtUtc == null)
            .ToListAsync(ct);
        foreach (var stale in outstanding)
            stale.UsedAtUtc = DateTime.UtcNow;
        if (outstanding.Count > 0)
            await _dbContext.SaveChangesAsync(ct);

        _dbContext.PhoneVerificationCodes.Add(new PhoneVerificationCode
        {
            UserId = user.Id,
            PhoneNumber = phoneNumber,
            CodeHash = TokenService.Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.Add(PhoneVerificationCodeConstants.Lifetime)
        });
        await _dbContext.SaveChangesAsync(ct);

        await _smsSender.SendAsync(
            phoneNumber,
            $"Seu código de verificação da conta é {code}. Não compartilhe.",
            ct);
    }

    public async Task<bool> ConfirmPhoneAsync(
        Guid userId,
        string phoneNumber,
        string code,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || code.Length != 6 || !code.All(char.IsDigit))
            return false;

        var hash = TokenService.Hash(code);
        var record = await _dbContext.PhoneVerificationCodes
            .SingleOrDefaultAsync(x =>
                x.UserId == user.Id &&
                x.PhoneNumber == phoneNumber &&
                x.CodeHash == hash &&
                x.UsedAtUtc == null, ct);

        if (record is null || record.ExpiresAtUtc <= DateTime.UtcNow)
            return false;

        record.UsedAtUtc = DateTime.UtcNow;

        // Código válido: grava o número e marca como confirmado.
        user.PhoneNumber = phoneNumber;
        user.PhoneNumberConfirmed = true;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
            await _dbContext.SaveChangesAsync(ct);

        return result.Succeeded;
    }

private async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        string token,
        CancellationToken ct)
    {
        var link = _emailOptions.ConfirmationUrlTemplate
            .Replace("{email}", WebUtility.UrlEncode(user.Email))
            .Replace("{token}", WebUtility.UrlEncode(token));

        var body = $"""
                <p>Olá, {WebUtility.HtmlEncode(user.FullName)}!</p>
                <p>Confirme seu e-mail para concluir o cadastro e ativar a conta:</p>
                <p><a href="{WebUtility.HtmlEncode(link)}">Confirmar e-mail</a></p>
                <p style="color:#888">Se não solicitou, ignore esta mensagem.</p>
                """;

        await _emailSender.SendAsync(
            user.Email!,
            "Confirme seu e-mail — Identity",
            body,
            ct);
    }

    // ---- Gestão de senha (Etapa 11) ----

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        // Resposta sempre "ok": não revela se o e-mail existe na plataforma.
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed)
            return;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        await SendPasswordResetEmailAsync(user, token, ct);
    }

    public async Task ForgotPasswordSmsAsync(string email, CancellationToken ct = default)
    {
        // Resposta sempre "ok" e código válido por 10 minutos.
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed)
            return;

        var code = GenerateNumericCode(6);

        // Invalida códigos anteriores do mesmo usuário antes de persistir o novo.
        var outstanding = await _dbContext.PasswordResetCodes
            .Where(x => x.UserId == user.Id && x.UsedAtUtc == null)
            .ToListAsync(ct);
        foreach (var stale in outstanding)
            stale.UsedAtUtc = DateTime.UtcNow;
        if (outstanding.Count > 0)
            await _dbContext.SaveChangesAsync(ct);

        _dbContext.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = TokenService.Hash(code),
            ExpiresAtUtc = DateTime.UtcNow.Add(PasswordResetCodeConstants.Lifetime)
        });
        await _dbContext.SaveChangesAsync(ct);

        await _smsSender.SendAsync(
            user.PhoneNumber ?? user.Email!,
            $"Seu código de redefinição de senha é {code}. Válido por 10 minutos.",
            ct);
    }

    public async Task<bool> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed)
            return false;

        // Suporta o token nativo do Identity (link do e-mail) ou o código SMS de
        // 6 dígitos persistido em password_reset_codes.
        var isNumericCode = token.Length == 6 && token.All(char.IsDigit);

        if (isNumericCode)
        {
            var hash = TokenService.Hash(token);
            var record = await _dbContext.PasswordResetCodes
                .SingleOrDefaultAsync(x =>
                    x.UserId == user.Id &&
                    x.CodeHash == hash &&
                    x.UsedAtUtc == null, ct);

            if (record is null || record.ExpiresAtUtc <= DateTime.UtcNow)
                return false;

            // Código válido: marca como usado e troca a senha gerando um token
            // nativo do Identity (aplica as políticas de senha do provider).
            record.UsedAtUtc = DateTime.UtcNow;

            var nativeToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(
                user, nativeToken, newPassword);

            if (result.Succeeded)
            {
                // Reset de senha é sensível: invalida todas as sessões.
                await _tokenService.RevokeAllUserRefreshTokensAsync(user.Id, ct);
                await _dbContext.SaveChangesAsync(ct);
            }

            return result.Succeeded;
        }

        var emailResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (emailResult.Succeeded)
            await _tokenService.RevokeAllUserRefreshTokensAsync(user.Id, ct);

        return emailResult.Succeeded;
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;

        var result = await _userManager.ChangePasswordAsync(
            user, currentPassword, newPassword);

        if (result.Succeeded)
            await _tokenService.RevokeAllUserRefreshTokensAsync(user.Id, ct);

        return result.Succeeded;
    }

    public async Task LogoutAsync(
        Guid userId,
        string refreshToken,
        CancellationToken ct = default)
    {
        // Revoga apenas a sessão informada, desde que pertença ao usuário.
        var pair = await _tokenService.ValidateRefreshTokenAsync(refreshToken, ct);
        if (pair is null || pair.Token.UserId != userId)
            return;

        await _tokenService.RevokeRefreshTokenAsync(pair, ct);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken ct = default)
        => await _tokenService.RevokeAllUserRefreshTokensAsync(userId, ct);

    // ---- Completa o cadastro do Client (onboarding pós-login social) ----

    public async Task<CompleteProfileResult> CompleteProfileAsync(
        Guid userId,
        CompleteProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return CompleteProfileResult.Fail("Usuário não encontrado.");

        if (user.TenantId is null)
            return CompleteProfileResult.Fail(
                "Apenas contas vinculadas a um tenant possuem cadastro para completar.");

        var document = string.IsNullOrWhiteSpace(request.Document)
            ? null
            : new string(request.Document.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(document))
            return CompleteProfileResult.Fail("O documento (CPF/CNPJ) é obrigatório para completar o cadastro.");

        // Unicidade do documento por tenant (Etapa 04), excluindo o próprio usuário.
        try
        {
            await _userUniqueness.EnsureDocumentUniqueAsync(user.TenantId, document, user.Id, ct);
        }
        catch (BusinessRuleViolationException ex)
        {
            return CompleteProfileResult.Fail(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        user.Document = document;

        var updated = await _userManager.UpdateAsync(user);
        if (!updated.Succeeded)
            return CompleteProfileResult.Fail(
                updated.Errors.Select(e => e.Description).ToArray());

        // Perfil alterado é ação sensível: invalida as sessões antigas e emite
        // um NOVO par com a claim profile_complete já atualizada (o access token
        // anterior continuava com a claim antiga até expirar).
        await _tokenService.RevokeAllUserRefreshTokensAsync(user.Id, ct);
        return CompleteProfileResult.Ok(await IssueTokensAsync(user, ct));
    }

    private async Task SendPasswordResetEmailAsync(
        ApplicationUser user,
        string token,
        CancellationToken ct)
    {
        var link = _emailOptions.PasswordResetUrlTemplate
            .Replace("{email}", WebUtility.UrlEncode(user.Email))
            .Replace("{token}", WebUtility.UrlEncode(token));

        var body = $"""
            <p>Olá, {WebUtility.HtmlEncode(user.FullName)}!</p>
            <p>Recebemos uma solicitação para redefinir sua senha.</p>
            <p><a href="{WebUtility.HtmlEncode(link)}">Redefinir senha</a></p>
            <p style="color:#888">Se não solicitou, ignore esta mensagem.</p>
            """;

        await _emailSender.SendAsync(
            user.Email!,
            "Redefinição de senha — Identity",
            body,
            ct);
    }

    private static string GenerateNumericCode(int digits)
    {
        // 6 dígitos com distribuição uniforme (evita o viés de Random).
        var buffer = new byte[digits];
        RandomNumberGenerator.Fill(buffer);
        return string.Concat(buffer.Select(b => (b % 10).ToString()));
    }
}