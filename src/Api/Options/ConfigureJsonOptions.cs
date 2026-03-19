using Api.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Api.Options;

public class ConfigureJsonOptions(IDataProtector protector) : IPostConfigureOptions<JsonOptions>
{
    public void PostConfigure(string? name, JsonOptions options)
    {
        options.SerializerOptions.PostConfigure(protector);
    }
}
