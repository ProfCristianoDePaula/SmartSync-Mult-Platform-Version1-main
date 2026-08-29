using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Estoque.Api.OpenApi;

/// <summary>
/// Registra o esquema de segurança "Bearer" (HTTP Bearer / JWT) no documento OpenAPI
/// 3.1 gerado pelo pacote nativo Microsoft.AspNetCore.OpenApi (Etapa 09).
///
/// A exigência de segurança por operação não é registrada aqui; ela fica a cargo do
/// <see cref="AuthorizeOperationTransformer"/> para que apenas endpoints que exigem
/// autenticação exibam o botão de autorização na UI (Scalar/Swagger). Os endpoints
/// públicos do Identity (login, refresh, confirmação de e-mail, JWKS, OAuth externo,
/// health) não exigem token e, portanto, não devem mostrar o esquema Bearer.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token emitido por este serviço (RS256). " +
                          "Use o botão Authorize e forneça o token: Authorization: Bearer {token}."
        };
    }
}
