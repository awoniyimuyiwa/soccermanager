using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Api.OpenApi;

public class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // context.JsonTypeInfo gives access to the underlying C# type
        if (context.JsonTypeInfo.Type.IsEnum)
        {
            var enumType = context.JsonTypeInfo.Type;

            schema.Enum ??= [];
            schema.Enum.Clear();

            // Populate the OpenAPI enum with string names
            foreach (var name in Enum.GetNames(enumType))
            {
                schema.Enum.Add(JsonValue.Create(name));
            }

            // Ensure the schema type is set to string
            schema.Type = JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }
}
