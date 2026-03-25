using Api.Attributes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.OpenApi;

public class IdempotencyHeaderOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Check if the endpoint has the IdempotentAttribute
        var isIdempotent = context.Description.ActionDescriptor.EndpointMetadata
            .Any(m => m is IdempotentAttribute);

        if (isIdempotent)
        {
            operation.Parameters ??= [];

            if (!operation.Parameters.Any(p => p.Name == Constants.IdempotencyKeyHeaderName))
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = Constants.IdempotencyKeyHeaderName,
                    In = ParameterLocation.Header,
                    Description = "Client-generated unique key to ensure request idempotency.",
                    Required = false, // Usually false so it doesn't break simple API tests
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid"
                    }
                });
            }
        }

        return Task.CompletedTask;
    }
}

