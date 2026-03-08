using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace Api.OpenApi;

public class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, 
        OpenApiSchemaTransformerContext context, 
        CancellationToken cancellationToken)
    {

        // 1. Check if we are currently processing a property (like 'Provider')
        // and look for the [EnumDataType] attribute on that property.
        var enumAttr = context.JsonPropertyInfo?.AttributeProvider?
            .GetCustomAttributes(typeof(EnumDataTypeAttribute), false)
            .FirstOrDefault() as EnumDataTypeAttribute;

        if (enumAttr != null)
        {
            var enumType = enumAttr.EnumType;
            var values = Enum.GetValues(enumType).Cast<int>();
            var names = Enum.GetNames(enumType);

            // Ensure we are working with an integer schema
            schema.Type = JsonSchemaType.Integer;
            schema.Enum ??= [];
            schema.Enum.Clear();

            // Add the valid integer IDs to the OpenAPI 'enum' list
            foreach (var value in values)
            {
                schema.Enum.Add(JsonValue.Create(value));
            }

            // Update the description so the UI (Swagger/Scalar) shows the mapping
            var mapping = string.Join(", ", names.Zip(values, (name, val) => $"{val}={name}"));
            schema.Description = $"Supported IDs: {mapping}";
        }

        return Task.CompletedTask;
    }
}
