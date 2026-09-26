using Fiscal.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fiscal.Api.ExceptionHandling;

/// <summary>
/// Handler global de exceções → ProblemDetails (R13, mensagens pt-BR).
/// Sem segredos em respostas: mensagens de exceção internas nunca vazam
/// detalhes (R4); o detalhe real vai só para o log com CorrelationId.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items["CorrelationId"]?.ToString();

        var (status, title, detail) = exception switch
        {
            BusinessRuleViolationException ex => (
                StatusCodes.Status400BadRequest, "Regra de negócio violada.", ex.Message),
            UnauthorizedAccessException ex => (
                StatusCodes.Status403Forbidden, "Acesso negado.", ex.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Erro interno.",
                "Ocorreu um erro inesperado. Tente novamente com o mesmo CorrelationId."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erro não tratado [{CorrelationId}].", correlationId);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status,
            Extensions = { ["correlationId"] = correlationId }
        }, cancellationToken);

        return true;
    }
}
