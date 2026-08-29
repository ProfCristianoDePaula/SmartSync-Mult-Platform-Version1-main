using Estoque.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.Api.Extensions;

/// <summary>
/// Base dos controllers com helpers de erro no MESMO contrato do Identity:
/// ProblemDetails; ValidationException → 400; BusinessRuleViolationException → 400;
/// recurso inexistente → 404 {title}; tenant/filial → 403.
/// </summary>
public abstract class EstoqueControllerBase : ControllerBase
{
    protected BadRequestObjectResult ValidationFailure(ValidationException ex)
        => BadRequest(new ProblemDetails
        {
            Title = "Dados inválidos.",
            Detail = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)),
            Status = StatusCodes.Status400BadRequest
        });

    protected BadRequestObjectResult BusinessRuleFailure(BusinessRuleViolationException ex)
        => BadRequest(new ProblemDetails
        {
            Title = "Regra de negócio violada.",
            Detail = ex.Message,
            Status = StatusCodes.Status400BadRequest
        });

    protected NotFoundObjectResult NotFound(string title)
        => NotFound(new { title });

    protected ObjectResult ForbiddenTenant()
        => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Acesso negado a este tenant.",
            Status = StatusCodes.Status403Forbidden
        });
}

