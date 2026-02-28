using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Api.Options;

public class ConfigureCookieAuthenticationOptions(
    IOptions<SecurityOptions> securityOptions,
    ITicketStore sessionStore)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(
        string? name,
        CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "__Host-Auth";

        options.ExpireTimeSpan = securityOptions.Value.CookieTimeout;

        options.SessionStore = sessionStore;

        // Fires when the user explicitly logs out or the session is invalidated.
        options.Events.OnSigningOut = context =>
        {
            // Manually delete the JS-readable token
            context.HttpContext.Response.Cookies.Delete(Constants.AntiforgeryJSReadableCookieName);

            // Manually delete the internal Antiforgery cookie 
            // (Don't trust the framework to do it 'automatically' with __Host- prefixes)
            context.HttpContext.Response.Cookies.Delete(
                Constants.AntiforgeryCookieName,
                new CookieOptions
                {
                    HttpOnly = true,
                    Path = "/",
                    Secure = true
                });

            return Task.CompletedTask;
        };
    }
}

