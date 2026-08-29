using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Estoque.Api.OpenApi;

/// <summary>
/// Aplica o requisito de segurança Bearer operação a operação: somente endpoints
/// que exigem autenticação (presença de <see cref="AuthorizeAttribute"/> nos
/// metadados do <see cref="ApiDescription"/>) recebem o requisito "Bearer".
///
/// O esquema em si é declarado pelo <see cref="BearerSecuritySchemeTransformer"/>
/// no nível do documento.
/// </summary>
internal sealed class AuthorizeOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var requiresAuthorization = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Any();

        if (requiresAuthorization && context.Document is not null)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }

        return Task.CompletedTask;
    }
}
