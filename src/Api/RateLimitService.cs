using Api.Options;
using Api.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using RedisRateLimiting;
using RedisRateLimiting.AspNetCore;
using StackExchange.Redis;
using System.Net;
using System.Threading.RateLimiting;

namespace Api;

/// <summary>
/// Provides centralized logic for distributed rate limiting using Redis.
/// Manages partition definitions for both global and user-based policies,
/// and handles unified 429 (Too Many Requests) responses across the application.
/// </summary>
/// <param name="connectionMultiplexer">The Redis connection used for distributed counters.</param>
/// <param name="rateLimitOptions">The configuration options defining limits and time windows.</param>
public class RateLimitService(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RateLimitOptions> rateLimitOptions)
{
    readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    readonly RateLimitOptions _rateLimitOptions = rateLimitOptions.Value;

    public TimeSpan Minutes => TimeSpan.FromMinutes(_rateLimitOptions.Minutes);

    public int GlobalLimit => _rateLimitOptions.GlobalLimit;

    /// <summary>
    /// Lua script to get both the current count and the oldest timestamp in a single call for the user policy
    /// </summary>
    const string UserPolicyMetadataScript = $$"""
        local current_count = redis.call('ZCOUNT', KEYS[1], '-inf', '+inf')
        local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
        return {current_count, oldest[2]}
        """;

    public RateLimitPartition<string> GetGlobalPolicyPartition(HttpContext context)
    {
        var path = context.Request.Path;
        var pathValue = path.Value ?? string.Empty;

        if (path.StartsWithSegments("/swagger") ||
            path.StartsWithSegments("/scalar") ||
            path.StartsWithSegments("/openapi"))
        {
            return RateLimitPartition.GetNoLimiter("internal_docs");
        }

        if (pathValue.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            pathValue.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("static_assets");
        }

        return RateLimitPartition.Get(Constants.GlobalRateLimitPolicyName, _ =>
            new RedisFixedWindowRateLimiter<string>(Constants.GlobalRateLimitPolicyName,
            new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => connectionMultiplexer,
                PermitLimit = GlobalLimit,
                Window = Minutes
            }));
    }

    public RateLimitPartition<string> GetUserPolicyPartition(HttpContext httpContext)
    {
        var (partitionKey, limit) = GetUserPolicyPartitionDetails(httpContext);

        if (IsBypassed(partitionKey))
        {
            return RateLimitPartition.GetNoLimiter(partitionKey);
        }

        return RateLimitPartition.Get(partitionKey, _ =>
            new RedisSlidingWindowRateLimiter<string>(partitionKey, new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => connectionMultiplexer,
                Window = Minutes,
                PermitLimit = limit
                // RedisRateLimiting Library: handles the "sliding" logic via Redis Lua scripts (often using ZSETs or timestamped keys), which typically calculates the window boundary dynamically without needing explicit segment counts from the user.
                //SegmentsPerWindow = 4 
            }));
    }

    public (string Key, int Limit) GetUserPolicyPartitionDetails(HttpContext context)
    {
        var identity = context.User.Identity;

        if (identity is { IsAuthenticated: true })
        {
            return (identity.Name ?? "auth-user", _rateLimitOptions.UserLimit);
        }

        var guestKey = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return (guestKey, _rateLimitOptions.GuestLimit);
    }

    /// <summary>
    /// The library adds "rl:sw:" as a prefix automatically
    /// key in Redis is actually rl:sw:{<paramref name="partitionKey"/>}
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <returns>Redis key</returns>
    public string GetUserPolicyRedisKey(string partitionKey) => $"rl:sw:{{{partitionKey}}}";

    public async Task<(long Remaining, long ResetUnix)> GetUserPolicyMetadata(string redisKey, int limit)
    {
        // Ensure the key is treated as a RedisKey type
        var keys = new RedisKey[] { (RedisKey)redisKey };

        // Execute the script
        var result = await _database.ScriptEvaluateAsync(UserPolicyMetadataScript, keys);

        var results = (RedisResult[])result!;

        var currentCount = results.Length > 0 ? (long)results[0] : 0;

        // results[1] is the score (timestamp in s) of the oldest entry
        // e.g 1771278721.938(Unix Seconds)
        double oldestScore = results.Length > 1 && !results[1].IsNull
            ? (double)results[1]
            : 0;

        // Use Seconds for everything
        double nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double windowSeconds = Minutes.TotalSeconds; // This should be 60, NOT 60000

        // Reset = (1771278721 + 60) = 1771278781
        double resetSeconds = oldestScore > 0
            ? oldestScore + windowSeconds
            : nowSeconds + windowSeconds;

        // Return as a 10-digit long
        return (Math.Max(0, limit - currentCount), (long)Math.Ceiling(resetSeconds));
    }

    public bool IsBypassed(string partitionKey) => 
        _rateLimitOptions.WhiteList.Contains(partitionKey)
        || (IPAddress.TryParse(partitionKey, out var ipAddress)
            && _rateLimitOptions.WhiteList.Any(w => w.Contains('/') && IPAddressHelper.IsInCidrRange(ipAddress, w)));

    public async ValueTask HandleOnRejected(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var leaseTypeName = context.Lease.GetType().FullName ?? string.Empty;
        var leaseType = context.Lease.GetType();
        var isUserPolicy = leaseType.DeclaringType is not null 
            && leaseType.DeclaringType.IsGenericType 
            && leaseType.DeclaringType.GetGenericTypeDefinition() == typeof(RedisSlidingWindowRateLimiter<>);

        httpContext.Response.Headers["X-RateLimit-Scope"] = isUserPolicy ? "User" : "Global";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Instance = httpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc6585#section-4"
        };

        if (isUserPolicy)
        {
            await RateLimitMetadata.OnRejected(
                context.HttpContext,
                context.Lease,
                cancellationToken);

            var retryAfter = context.HttpContext.Response.Headers.RetryAfter;

            // Check if the value is empty to provide a fallback
            var displaySeconds = StringValues.IsNullOrEmpty(retryAfter)
                ? ((int)Minutes.TotalSeconds).ToString()
                : retryAfter.ToString();

            problemDetails.Title = "Too Many Requests";
            problemDetails.Detail = $"Quota exceeded. Please try again in {displaySeconds} seconds.";           
        }
        else
        {
            // Don't reveal too much detail
            problemDetails.Title = "Server Capacity Exceeded";
            problemDetails.Detail = "The server is currently under high load. Please try again soon.";
            var retryAfter = ((int)Minutes.TotalSeconds).ToString();
            context.HttpContext.Response.Headers.RetryAfter = retryAfter;
        }

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);
    }
}
