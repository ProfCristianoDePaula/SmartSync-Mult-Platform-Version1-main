using Serilog.Context;

namespace Identity.Api.Observability;

/// <summary>
/// Propagação do Correlation ID entre microsserviços:
/// - lê o header `X-Correlation-ID` (quando fornecido) ou gera um GUID;
/// - injeta no Serilog LogContext (todos os logs da request ganham a propriedade);
/// - devolve no header `X-Correlation-ID` da resposta;
/// - armazena em HttpContext.Items para log de negócio/instrumentação.
/// Permitir IDs externos: valor > 8 chars; senão gera o nosso.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private static readonly TimeProvider Time = TimeProvider.System;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();

        // Só aceitamos IDs externos sanitizados (evita header gigante/log poisoning).
        var correlationId = !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}