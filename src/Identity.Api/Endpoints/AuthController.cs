using System.Security.Claims;
using Identity.Application.Auth;
using Identity.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.Api.Endpoints;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (result is null)
            return Unauthorized(new ProblemDetails
            {
                Title = "Credenciais inválidas",
                Detail = "E-mail/usuário e senha não conferem ou a conta não está confirmada.",
                Status = StatusCodes.Status401Unauthorized
            });

        return Ok(result);
    }

    /// <summary>
    /// Cadastro público de Client (autosserviço) vinculado a um tenant.
    /// PÚBLICO — cria a conta com e-mail não confirmado e envia o link de
    /// confirmação; NÃO emite tokens (o login exige e-mail confirmado).
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(new ProblemDetails
            {
                Title = "Não foi possível cadastrar a conta.",
                Detail = result.Errors is not null ? string.Join(" ", result.Errors) : null,
                Status = StatusCodes.Status400BadRequest
            });

        return StatusCode(StatusCodes.Status201Created,
            new { title = "Conta criada. Confirme o e-mail para ativar." });
    }

    [HttpPost("refresh-token")]
    [EnableRateLimiting("auth-refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(request, ct);
        if (result is null)
            return Unauthorized(new { title = "Refresh token inválido ou expirado." });

        return Ok(result);
    }

    // ============ Etapa 06: confirmação de e-mail e validação de celular ============

    /// <summary>Reenvia o e-mail de confirmação (resposta neutra, sem vazar existência).</summary>
    [HttpPost("resend-confirmation-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendConfirmationEmail(
        [FromBody] ResendConfirmationEmailRequest request,
        CancellationToken ct)
    {
        await _authService.ResendConfirmationEmailAsync(request.Email, ct);
        return Ok(new { title = "Se o e-mail existir e não estiver confirmado, o link foi reenviado." });
    }

    /// <summary>Confirma o e-mail com o token nativo do Identity recebido no link.</summary>
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request,
        CancellationToken ct)
    {
        var confirmed = await _authService.ConfirmEmailAsync(request.Email, request.Token, ct);
        if (!confirmed)
            return BadRequest(new { title = "Link de confirmação inválido ou já utilizado." });

        return Ok(new { title = "E-mail confirmado. Conta ativa." });
    }

    /// <summary>
    /// Mesma confirmação, mas via GET — alvo do link clicado no e-mail.
    /// Devolve uma página HTML simples em vez de JSON.
    /// </summary>
    [HttpGet("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmailFromLink(
        [FromQuery] string email,
        [FromQuery] string token,
        CancellationToken ct)
    {
        var confirmed = await _authService.ConfirmEmailAsync(email, token, ct);

        var html = confirmed
            ? """
              <!DOCTYPE html><html lang="pt-BR"><head><meta charset="utf-8">
              <title>E-mail confirmado</title></head>
              <body style="font-family:sans-serif;text-align:center;margin-top:10%">
              <h2>E-mail confirmado. Conta ativa.</h2>
              <p>Você já pode fazer login.</p></body></html>
              """
            : """
              <!DOCTYPE html><html lang="pt-BR"><head><meta charset="utf-8">
              <title>Link inválido</title></head>
              <body style="font-family:sans-serif;text-align:center;margin-top:10%">
              <h2>Link de confirmação inválido ou já utilizado.</h2>
              <p>Solicite um novo e-mail de confirmação.</p></body></html>
              """;

        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Gera o código SMS (token nativo) e envia ao celular informado.
    /// AUTENTICADO e exige e-mail confirmado (policy email-confirmed).
    /// </summary>
    [HttpPost("send-sms-code")]
    [Authorize(Policy = "email-confirmed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SendSmsCode(
        [FromBody] SendSmsCodeRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized(new { title = "Token inválido: usuário não identificado." });

        await _authService.SendSmsCodeAsync(userId.Value, request.PhoneNumber, ct);
        return Ok(new { title = "Código enviado. Confira o SMS e a validação." });
    }

    /// <summary>Confirma o celular validando o código SMS e persistindo o número.</summary>
    [HttpPost("confirm-phone")]
    [Authorize(Policy = "email-confirmed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmPhone(
        [FromBody] ConfirmPhoneRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized(new { title = "Token inválido: usuário não identificado." });

        var confirmed = await _authService.ConfirmPhoneAsync(
            userId.Value, request.PhoneNumber, request.Code, ct);
        if (!confirmed)
            return BadRequest(new { title = "Código inválido ou expirado para este telefone." });

        return Ok(new { title = "Celular validado e salvo na conta." });
    }

    // ============ Etapa 11: gestão de senha, logout e sessões (todas as roles) ============

    /// <summary>
    /// Gera o token de reset (nativo do Identity) e envia o link por e-mail.
    /// PÚBLICO — resposta neutra para não vazar a existência da conta.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-recovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request.Email, ct);
        return Ok(new { title = "Se o e-mail existir, o link de redefinição foi enviado." });
    }

    /// <summary>
    /// Gera o código de redefinição (6 dígitos, válido por 10 min) e envia por
    /// SMS ao celular cadastrado. PÚBLICO — resposta neutra.
    /// </summary>
    [HttpPost("forgot-password/sms")]
    [EnableRateLimiting("auth-recovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordSms(
        [FromBody] ForgotPasswordSmsRequest request,
        CancellationToken ct)
    {
        await _authService.ForgotPasswordSmsAsync(request.Email, ct);
        return Ok(new { title = "Se o e-mail existir, o código foi enviado." });
    }

    /// <summary>
    /// Efetiva a troca aceitando o token do link do e-mail OU o código SMS de
    /// 6 dígitos. PÚBLICO via token/código.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-recovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken ct)
    {
        var reset = await _authService.ResetPasswordAsync(
            request.Email, request.Token, request.NewPassword, ct);
        if (!reset)
            return BadRequest(new { title = "Token/código inválido, expirado ou senha fora das regras." });

        return Ok(new { title = "Senha redefinida. Use a nova senha para entrar." });
    }

    /// <summary>
    /// Troca de senha com sessão ativa (exige a senha atual). Revoga os demais
    /// refresh tokens do usuário. AUTENTICADO (qualquer role).
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized(new { title = "Token inválido: usuário não identificado." });

        var changed = await _authService.ChangePasswordAsync(
            userId.Value, request.CurrentPassword, request.NewPassword, ct);
        if (!changed)
            return BadRequest(new { title = "Senha atual incorreta ou nova senha fora das regras." });

        return Ok(new { title = "Senha alterada. As demais sessões foram revogadas." });
    }

    /// <summary>
    /// Logout de uma sessão: revoga o refresh token informado. AUTENTICADO.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized(new { title = "Token inválido: usuário não identificado." });

        await _authService.LogoutAsync(userId.Value, request.RefreshToken, ct);
        return Ok(new { title = "Sessão encerrada." });
    }

    /// <summary>Logout de todas as sessões do usuário. AUTENTICADO.</summary>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized(new { title = "Token inválido: usuário não identificado." });

        await _authService.LogoutAllAsync(userId.Value, ct);
        return Ok(new { title = "Todas as sessões foram encerradas." });
    }

    /// <summary>
    /// Completa o cadastro do Client autenticado (documento CPF/CNPJ obrigatório;
    /// nome opcional) — etapa de onboarding após o login social. Reemite o par de
    /// tokens com a claim profile_complete atualizada. EXCLUSIVO da role Client.
    /// </summary>
    [HttpPost("profile")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteProfile(
        [FromBody] CompleteProfileRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null)
            return Unauthorized(new { title = "Token inválido: usuário não identificado." });

        var result = await _authService.CompleteProfileAsync(userId.Value, request, ct);
        if (!result.Succeeded)
            return BadRequest(new ProblemDetails
            {
                Title = "Não foi possível completar o cadastro.",
                Detail = result.Errors is not null ? string.Join(" ", result.Errors) : null,
                Status = StatusCodes.Status400BadRequest
            });

        return Ok(result.Token);
    }

    private Guid? CurrentUserId()
        => User.Identity is not null && User.Identity.IsAuthenticated
            && Guid.TryParse(User.FindFirstValue(JwtClaims.UserId), out var id)
                ? id
                : null;
}