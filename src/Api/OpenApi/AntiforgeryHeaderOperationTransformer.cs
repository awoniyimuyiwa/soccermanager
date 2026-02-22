using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.OpenApi;

public class AntiforgeryHeaderOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, 
        OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var httpMethod = context.Description.HttpMethod;

        // Skip adding the header for "Safe" methods (GET, HEAD, OPTIONS, TRACE)
        if (string.IsNullOrEmpty(httpMethod) 
            || HttpMethods.IsGet(httpMethod) 
            || HttpMethods.IsHead(httpMethod) 
            || HttpMethods.IsOptions(httpMethod) 
            || HttpMethods.IsTrace(httpMethod))
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];

        if (!operation.Parameters.Any(p => p.Name == Constants.AntiforgeryHeaderName))
        {
            var parameter = new OpenApiParameter
            {
                Name = Constants.AntiforgeryHeaderName,
                In = ParameterLocation.Header,
                Description = "Antiforgery token required for cookie-based state-changing requests.",
                Required = false,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String
                }
            };

            if (context.Description.RelativePath?.Contains("login") == true)
            {
                parameter.Description += " (Required only if useCookies is true)";
            }

            operation.Parameters.Add(parameter);
        }

        return Task.CompletedTask;
    }
}
