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
            Constants.AntiforgeryCookieName,
            tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax
            });
    }

    /// <summary>
    /// Determines if the current request should bypass antiforgery validation.
    /// Skips validation if:
    /// <list type="bullet">
    /// <item>The endpoint is explicitly marked with <see cref="IAntiforgeryMetadata"/> (e.g., [IgnoreAntiforgeryToken]).</item>
    /// <item>The request uses a "safe" HTTP method (GET, HEAD, OPTIONS, TRACE).</item>
    /// <item>The request uses Bearer Authentication, which is inherently protected against CSRF.</item>
    /// </list>
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <returns><c>true</c> if validation should be skipped; otherwise, <c>false</c>.</returns>
    public static bool ShouldSkipAntiforgeryValidation(this HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var antiforgeryMetadata = endpoint?.Metadata.GetMetadata<IAntiforgeryMetadata>();
        var authHeader = httpContext.Request.Headers.Authorization.ToString();
     
        var method = httpContext.Request.Method;
        if (antiforgeryMetadata?.RequiresValidation == false
            || HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method)
            || authHeader.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
} 
    
