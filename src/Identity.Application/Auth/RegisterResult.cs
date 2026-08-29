namespace Identity.Application.Auth;

/// <summary>
/// Resultado do cadastro público. Em sucesso não emite tokens (a conta precisa
/// de confirmação de e-mail antes de logar). Em falha, lista erros legíveis
/// (unicidade, regras de senha etc.).
/// </summary>
public sealed record RegisterResult(bool Succeeded, IReadOnlyList<string>? Errors = null)
{
    public static RegisterResult Ok() => new(true);
    public static RegisterResult Fail(params string[] errors) => new(false, errors);
}