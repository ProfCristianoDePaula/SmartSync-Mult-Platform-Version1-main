namespace Fiscal.Domain.Common;

/// <summary>
/// Violação de uma regra de negócio.
/// Deve ter mensagem clara para o usuário em pt-BR.
/// </summary>
public sealed class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message)
        : base(message)
    {
    }

    public BusinessRuleViolationException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    public string? Code { get; }
}
