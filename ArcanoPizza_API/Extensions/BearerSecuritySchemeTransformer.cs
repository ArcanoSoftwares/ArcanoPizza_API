using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ArcanoPizza_API.Extensions;

/// <summary>
/// Declara el esquema HTTP Bearer (JWT) en el documento OpenAPI para que Swagger UI
/// muestre Authorize y envíe <c>Authorization: Bearer {token}</c>.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Pega solo el JWT de POST /api/Auth/login (campo accessToken). No escribas la palabra Bearer."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    }
}

/// <summary>
/// Quita el candado en endpoints anónimos o públicos; lo deja en los que tienen [Authorize].
/// </summary>
internal sealed class BearerSecurityRequirementTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (HasAttribute<AllowAnonymousAttribute>(context) || !HasAttribute<IAuthorizeData>(context))
        {
            operation.Security = [];
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer")] = []
        });
        return Task.CompletedTask;
    }

    private static bool HasAttribute<T>(OpenApiOperationTransformerContext context)
    {
        var descriptor = context.Description.ActionDescriptor;
        if (descriptor.EndpointMetadata.OfType<T>().Any())
            return true;

        if (descriptor is not ControllerActionDescriptor cad)
            return false;

        if (cad.MethodInfo.GetCustomAttributes(true).OfType<T>().Any())
            return true;

        return cad.ControllerTypeInfo.GetCustomAttributes(true).OfType<T>().Any();
    }
}
