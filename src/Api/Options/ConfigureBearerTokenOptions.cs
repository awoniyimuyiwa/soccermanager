using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Options;

namespace Api.Options;

public class ConfigureBearerTokenOptions(ISecureDataFormat<AuthenticationTicket> secureDataFormat)
    : IPostConfigureOptions<BearerTokenOptions>
{
    public void PostConfigure(
        string? name,
        BearerTokenOptions options)
    {
        options.BearerTokenExpiration = TimeSpan.FromHours(1);
        options.RefreshTokenExpiration = TimeSpan.FromDays(14);

        options.BearerTokenProtector = secureDataFormat;

        options.RefreshTokenProtector = secureDataFormat;
    }
}