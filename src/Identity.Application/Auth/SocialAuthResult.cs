namespace Identity.Application.Auth;

/// <summary>
/// Motivo pelo qual o login social não foi concluído. Cada valor é mapeado para
/// um código HTTP específico no controller (Etapa 21) — o antigo `null` genérico
/// escondia conflitos reais de e-mail, tenant inexistente e ausência de role.
/// </summary>
public enum SocialAuthError
{
    /// <summary>Provider não devolveu e-mail/providerKey (request inválido).</summary>
    InvalidRequest,

    /// <summary>Provedor social não suportado (scheme não registrado).</summary>
    ProviderUnsupported,

    /// <summary>Tenant não informado no request (query string / state).</summary>
    MissingTenantId,

    /// <summary>Tenant informado não existe.</summary>
    TenantNotFound,

    /// <summary>E-mail já existe na plataforma mas nunca foi vinculado a este
    /// provedor — bloqueio anti-takeover (Etapa 05), agora diferenciado.</summary>
    EmailConflict,

    /// <summary>Falha ao persistir o usuário/role/vínculo (criação atômica).</summary>
    CreateFailed,

    /// <summary>Conta encontrada/vinculada mas sem a role Client.</summary>
    NotClient,

    /// <summary>Conta vinculada ao provedor pertence a outro tenant.</summary>
    TenantMismatch
}

/// <summary>
/// Resultado do login social: um par de tokens (sucesso) ou um erro tipado.
/// Substitui o antigo `TokenResponse?` em que todo fracasso era `null` (401
/// genérico) e não havia distinção entre conflito, request inválido e falta de
/// autorização.
/// </summary>
public sealed record SocialAuthResult(TokenResponse? Token, SocialAuthError? Error)
{
    public static SocialAuthResult Ok(TokenResponse token) => new(token, null);

    public static SocialAuthResult Fail(SocialAuthError error) => new(null, error);
}
