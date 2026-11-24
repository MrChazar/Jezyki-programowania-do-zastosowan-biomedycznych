using Microsoft.OpenApi;

namespace HeartRate.Api;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
            };
            foreach (var (path, item) in document.Paths)
            {
                if (path.TrimEnd('/') is "/api/auth/register" or "/api/auth/login" or "/api/auth/refresh") continue;
                if (item.Operations is null) continue;
                foreach (var operation in item.Operations.Values)
                    operation.Security = [new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                    }];
            }
            return Task.CompletedTask;
        }));
        return services;
    }
}
