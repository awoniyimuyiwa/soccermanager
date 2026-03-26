using Microsoft.AspNetCore.Antiforgery;

namespace Api.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Generates and stores the antiforgery tokens within the current request.
    /// This operation is idempotent for a single session; calling this multiple times 
    /// will yield valid tokens as long as the user's identity (e.g., NameIdentifier claim) 
    /// remains unchanged.
    /// </summary>
    /// <param name="httpContext"></param>
    public static void GetAndStoreAntiforgeryToken(this HttpContext httpContext)
    {
        var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(httpContext);

        httpContext.Response.Cookies.Append(
            Constants.AntiforgeryJSReadableCookieName,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = true
            });
    }
} 
    
