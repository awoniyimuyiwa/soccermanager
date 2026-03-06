using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.ComponentModel.DataAnnotations;

namespace Api.OpenApi;

public class PasswordSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        // Check if the property has [DataType(DataType.Password)]
        var dataTypeAttr = context.JsonPropertyInfo?.AttributeProvider?
            .GetCustomAttributes(typeof(DataTypeAttribute), false)
            .OfType<DataTypeAttribute>()
            .FirstOrDefault(x => x.DataType == DataType.Password);

        if (dataTypeAttr != null)
        {
            // This is the specific OpenAPI field that triggers 
            // the "dots" (masked input) in Swagger and Scalar UIs
            schema.Format = "password";
        }

        return Task.CompletedTask;
    }
}
