using Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.MiddleWares;

public class RateLimitHeadersMiddleware(RequestDelegate next)
{
    readonly RequestDelegate _next = next;

    public async Task InvokeAsync(
        HttpContext httpContext,
        RateLimitService rateLimitService)
    {
        var (partitionKey, limit) = rateLimitService.GetPartitionDetails(httpContext);
        if (rateLimitService.IsBypassed(partitionKey))
        {
            await _next(httpContext);
            return;
        }

        var endpoint = httpContext.GetEndpoint();
        var rateLimitMetadata = endpoint?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
        // Check for the internal metadata if using RequireRateLimiting()
        var hasRateLimiting = endpoint?.Metadata.GetMetadata<IRateLimiterPolicy<string>>() is not null                  
            || rateLimitMetadata is not null;
        if (!hasRateLimiting)
        {
            await _next(httpContext);
            return;
        }

        // Add rate limit headers for UI frameworks
        httpContext.Response.OnStarting(async () =>
        {
            if (httpContext.Response.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                if (!httpContext.Response.Headers.ContainsKey("X-RateLimit-Scope"))
                {
                    httpContext.Response.Headers["X-RateLimit-Scope"] = "Global";
                }
                return;
            }

            try
            {
                var redisKey = rateLimitService.GetRedisKey(partitionKey);
                var (remaining, reset) = await rateLimitService.GetMetadata(redisKey, limit);

                var headers = httpContext.Response.Headers;
                headers["X-RateLimit-Limit"] = limit.ToString();
                headers["X-RateLimit-Remaining"] = remaining.ToString();
                headers["X-RateLimit-Reset"] = reset.ToString();
                headers["X-RateLimit-Scope"] = "User"; // Indicates the user's personal quota
            }
            catch (Exception)
            {
                // Fail silently so a Redis glitch doesn't crash a successful API response.
            }
        });

       await _next(httpContext);  
    }
}

