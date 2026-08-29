namespace Identity.Application.Auth;

/// <summary>
/// Resultado da conclusão do cadastro do Client. Em sucesso carrega o NOVO par
/// de tokens (access com a claim profile_complete=true), pois a versão anterior
/// ainda carregava a claim antiga. Em falha, lista erros legíveis.
/// </summary>
public sealed record CompleteProfileResult(
    bool Succeeded,
    TokenResponse? Token = null,
    IReadOnlyList<string>? Errors = null)
{
    public static CompleteProfileResult Ok(TokenResponse token) => new(true, token);
    public static CompleteProfileResult Fail(params string[] errors) => new(false, null, errors);
}
