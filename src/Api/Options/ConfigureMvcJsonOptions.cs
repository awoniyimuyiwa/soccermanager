using Api.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Api.Options;

public class ConfigureMvcJsonOptions(IDataProtector protector) : IPostConfigureOptions<Microsoft.AspNetCore.Mvc.JsonOptions>
{
    public void PostConfigure(string? name, Microsoft.AspNetCore.Mvc.JsonOptions options)
    {
        // Controllers use 'JsonSerializerOptions' property name, not 'SerializerOptions'
        options.JsonSerializerOptions.PostConfigure(protector);
    }
}
