using Api.Utils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Api.Options;

public class ConfigureJsonOptions(IDataProtector protector) : IPostConfigureOptions<JsonOptions>
{
    public void PostConfigure(string? name, JsonOptions options)
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // Add the modifier to the existing resolver (or create a new one)
        var resolver = options.SerializerOptions.TypeInfoResolver as DefaultJsonTypeInfoResolver        
            ?? new DefaultJsonTypeInfoResolver();

        resolver.Modifiers.Add(typeInfo => MaskedJsonModifier.Modify(typeInfo, protector));

        options.SerializerOptions.TypeInfoResolver = resolver;
    }
}
