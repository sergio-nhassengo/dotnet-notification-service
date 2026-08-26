using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;


namespace MPDCApiTemplate.OpenApi;

public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by POST /api/Auth/login."
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = []
        };

        foreach (var operation in document.Paths.Values
                     .Where(path => path.Operations is not null)
                     .SelectMany(path => path.Operations!.Values))
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(requirement);
        }

        return Task.CompletedTask;
    }
}
