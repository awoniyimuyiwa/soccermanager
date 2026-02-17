using Api.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.RateLimiting;

public class RateLimitService(
    IConnectionMultiplexer conectionMultiplexer,
    IOptions<RateLimitOptions> options)
{
    readonly IDatabase _database = conectionMultiplexer.GetDatabase();
    readonly RateLimitOptions _rateLimitOptions = options.Value;

    public TimeSpan Minutes => TimeSpan.FromMinutes(_rateLimitOptions.Minutes);

    public int GlobalLimit => _rateLimitOptions.GlobalLimit;

    /// <summary>
    /// Lua script to get both the current count and the oldest timestamp in a single call
    /// </summary>
    const string MetadataScript = $$"""
        local current_count = redis.call('ZCOUNT', KEYS[1], '-inf', '+inf')
        local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
        return {current_count, oldest[2]}
        """;

    public (string Key, int Limit) GetPartitionDetails(HttpContext context)
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
    public string GetRedisKey(string partitionKey) => $"rl:sw:{{{partitionKey}}}";

    public async Task<(long Remaining, long ResetUnix)> GetMetadata(string redisKey, int limit)
    {
        // Ensure the key is treated as a RedisKey type
        var keys = new RedisKey[] { (RedisKey)redisKey };

        // Execute the script
        var result = await _database.ScriptEvaluateAsync(MetadataScript, keys);

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
        _rateLimitOptions.WhiteList.Contains(partitionKey);
}