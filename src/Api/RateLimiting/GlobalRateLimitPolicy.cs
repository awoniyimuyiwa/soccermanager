using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedisRateLimiting;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace Api.RateLimiting;

public class GlobalRateLimitPolicy(
    IConnectionMultiplexer connectionMultiplexer,
    RateLimitService rateLimitService) : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        return RateLimitPartition.Get(Constants.GlobalRateLimitPolicyName, _ =>
            new RedisFixedWindowRateLimiter<string>(Constants.GlobalRateLimitPolicyName, new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => connectionMultiplexer,
                PermitLimit = rateLimitService.GlobalLimit,
                Window = rateLimitService.Minutes
            }));
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["X-RateLimit-Scope"] = "Global";

        var retryAfter = ((int)rateLimitService.Minutes.TotalSeconds).ToString();
        context.HttpContext.Response.Headers.RetryAfter = retryAfter;

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Server Capacity Exceeded",
            Detail = "The server is currently under high load. Please try again soon.",
            Instance = context.HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        }, cancellationToken: token);
    };
}

