using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;
using RedisRateLimiting;
using RedisRateLimiting.AspNetCore;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace Api.RateLimiting;

public class UserRateLimitPolicy(
    IConnectionMultiplexer connectionMultiplexer,
    RateLimitService rateLimitService) : IRateLimiterPolicy<string>
{
    readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
    readonly RateLimitService _rateLimitService = rateLimitService;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var (partitionKey, limit) = _rateLimitService.GetPartitionDetails(httpContext);

        if (_rateLimitService.IsBypassed(partitionKey))
        {
            // Returns a special partition that never limits
            return RateLimitPartition.GetNoLimiter(partitionKey);
        }

        // Return Redis rate limiter
        return RateLimitPartition.Get(partitionKey, factory =>
        {
            return new RedisSlidingWindowRateLimiter<string>(factory, new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => _connectionMultiplexer,
                Window = _rateLimitService.Minutes,
                PermitLimit = limit,
                // RedisRateLimiting Library: handles the "sliding" logic via Redis Lua scripts (often using ZSETs or timestamped keys), which typically calculates the window boundary dynamically without needing explicit segment counts from the user.
                //SegmentsPerWindow = 4 
            });
        });
    }

    /// <summary>
    /// Uses the library's built-in helper to generate a standard 429 Too Many Requests response with a Retry-After header. 
    /// This can be customized to return a different response or to log rejections.
    /// </summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, token) =>
    {
        // Let the library add the X-RateLimit headers to the response automatically
        await RateLimitMetadata.OnRejected(context.HttpContext, context.Lease, token);

        var retryAfter = context.HttpContext.Response.Headers.RetryAfter;

        // 1. Check if the value is empty to provide a fallback
        var displaySeconds = StringValues.IsNullOrEmpty(retryAfter)
            ? "60"
            : retryAfter.ToString();

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = $"Quota exceeded. Please try again in {displaySeconds} seconds.",
            Instance = context.HttpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        }, cancellationToken: token);
    };
}
