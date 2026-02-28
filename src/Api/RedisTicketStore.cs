using Api.Extensions;
using Api.Options;
using Domain;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using UAParser;

namespace Api;

/// <summary>
/// Implements <see cref="ITicketStore"/> and <see cref="IUserSessionManager"/> to manage 
/// Identity authentication tickets and session metadata within Redis.
/// </summary>
/// <remarks>
/// <para><strong>Key Security Benefits:</strong></para>
/// <list type="bullet">
/// <item>
/// <description><strong>Opaque Tokenization:</strong> Replaces "fat" encrypted cookies or tokens with random Session IDs. 
/// Sensitive claims (Roles, Internal IDs) remain server-side, never leaving the secure environment.</description>
/// </item>
/// <item>
/// <description><strong>Replay Attack Mitigation:</strong> Since the store is the authoritative source of truth, 
/// a token is only valid if its entry exists in Redis. Once a session is rotated or logged out, 
/// the old token is instantly neutralized, rendering stolen copies useless.</description>
/// </item>
/// <item>
/// <description><strong>Instant Global Revocation:</strong> Enables immediate invalidation of all sessions 
/// (including Bearer and Refresh tokens) across all devices, critical for password resets or security breaches.</description>
/// </item>
/// <item>
/// <description><strong>Concurrency Enforcement:</strong> Atomic Lua scripts prevent "zombie" sessions from 
/// blocking new logins when <see cref="LoginConcurrencyMode.Block"/> is active.</description>
/// </item>
/// </list>
/// </remarks>
/// <param name="database">Redis database used for low-level atomic operations.</param>
/// <param name="httpContextAccessor">Provides access to request headers and IP addresses for metadata tracking.</param>
/// <param name="geoReader">MaxMind database reader to resolve IP addresses to geographical locations.</param>
/// <param name="securityOptions">Application-specific security configurations like Concurrency Modes.</param>
/// <param name="timeProvider">Abstraction for time to ensure accurate expiry calculations and testability.</param>
/// <param name="uaParser">Parser to translate raw User-Agents into friendly device descriptions.</param>
public class RedisTicketStore(
    IDatabase database,
    IGeoIP2DatabaseReader geoReader,
    IHttpContextAccessor httpContextAccessor,
    IOptionsMonitor<SecurityOptions> securityOptions,
    TimeProvider timeProvider,
    Parser uaParser) : ITicketStore, ISecureDataFormat<AuthenticationTicket>, IUserSessionManager
{
    readonly IDatabase _database = database;
    readonly IGeoIP2DatabaseReader _geoReader = geoReader;
    readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    readonly IOptionsMonitor<SecurityOptions> _securityOptions = securityOptions;
    readonly TimeProvider _timeProvider = timeProvider;
    readonly Parser _uaParser = uaParser;
   
    readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false // Minify JSON to save memory
    };

    public const string KeyPrefix = "AuthSession:";
    public const string UserSessionPrefix = "UserSessions:";
    public const string UserExpiryPrefix = "UserExpiry:";

    public const string GenericDeviceInfo = "Unknown Device";
    public const string GenericLocation = "Unknown Location";
    public const string GetAndPruneScriptName = "GetAndPruneSessions";
    public const string RemoveAndSyncScriptName = "RemoveSessionAndSync";
    public const string RemoveAllSessionsScriptName = "RemoveAllSessions";
    public const string RemoveUnrelatedAndSyncScriptName = "RemoveUnrelatedAndSync";
    public const string StoreAndSyncScriptName = "StoreAndSyncSessions";

    public const string SessionCorrelationKey = "SessionCorrelationId";

    /// <summary>
    /// This script adds the sessionId to the Sorted Set (with its expiry score) and immediately updates the TTL of both the Hash and Sorted Set to match the new "latest" session.
    /// It ensures that adding/updating a session and synchronizing the group TTL happens in a single server-side operation. 
    /// This prevents "race conditions" where one session might accidentally expire the entire group while another is trying to extend it.
    /// </summary>
    const string StoreAndSyncScript = $$"""
        -- Script Name {{StoreAndSyncScriptName}}
        -- KEYS[1]: Individual Session Key (e.g., "AuthSession:sessionId")
        -- KEYS[2]: User Sessions Hash (e.g., "UserSession:userId")
        -- KEYS[3]: User Expiry Sorted Set (e.g., "UserExpiry:userId")

        -- ARGV[1]: Session ID
        -- ARGV[2]: Expiry Timestamp (Score/Seconds)
        -- ARGV[3]: Current Timestamp (Now)
        -- ARGV[4]: UserSession Metadata JSON
        -- ARGV[5]: Serialized Ticket Data

        -- Store the actual session ticket data
        redis.call('SET', KEYS[1], ARGV[5])

        -- Update the user-to-session mapping (Hash)
        redis.call('HSET', KEYS[2], ARGV[1], ARGV[4])

        -- Update the expiry tracking (Sorted Set)
        redis.call('ZADD', KEYS[3], ARGV[2], ARGV[1])

        -- Calculate the relative TTL based on the furthest expiration in the group
        local latest = redis.call('ZREVRANGE', KEYS[3], 0, 0, 'WITHSCORES')
        if #latest > 0 then
            local latestScore = tonumber(latest[2])
            local ttlSeconds = latestScore - tonumber(ARGV[3])

            if ttlSeconds > 0 then
                -- Set TTL for the individual session
                redis.call('EXPIRE', KEYS[1], ttlSeconds)
                -- Sync TTL for the grouping keys
                redis.call('EXPIRE', KEYS[2], ttlSeconds)
                redis.call('EXPIRE', KEYS[3], ttlSeconds)
            end
        end

        return 1
        """;

    /// <summary>
    /// Script to prune expired sessions and return remaining active sessions
    /// </summary>
    const string GetAndPruneScript = $$"""  
        -- Script Name {{GetAndPruneScriptName}}
        local userMappingKey = KEYS[1]
        local userExpiryKey = KEYS[2]
        local fullPrefixedPrefix = KEYS[3]
        local now = tonumber(ARGV[1])
        
        -- Fetch expired session IDs
        local expiredIds = redis.call('ZRANGEBYSCORE', userExpiryKey, '-inf', now)

        if #expiredIds > 0 then
            local authKeys = {}
            for i, id in ipairs(expiredIds) do
                table.insert(authKeys, fullPrefixedPrefix .. id)
                redis.call('HDEL', userMappingKey, id)

                -- Batch UNLINK every 100 keys to prevent stack overflow and memory spikes
                if #authKeys >= 100 then
                    redis.call('UNLINK', unpack(authKeys))
                    authKeys = {}
                end
            end

            -- Unlink any remaining keys in the table
            if #authKeys > 0 then
                redis.call('UNLINK', unpack(authKeys))
            end

            -- Remove expired sessions from the Sorted Set
            redis.call('ZREMRANGEBYSCORE', userExpiryKey, '-inf', now)

            -- Sync TTL
            local latest = redis.call('ZREVRANGE', userExpiryKey, 0, 0, 'WITHSCORES')
            if #latest > 0 then
                local ttl = tonumber(latest[2]) - now
                if ttl > 0 then
                    redis.call('EXPIRE', userMappingKey, ttl)
                    redis.call('EXPIRE', userExpiryKey, ttl)
                end
            else
                redis.call('UNLINK', userMappingKey, userExpiryKey)
            end
        end

        return redis.call('HVALS', userMappingKey)
        """;

    /// <summary>
    /// Script to Remove a session, related sessions with the same correlation id and update the group TTL to the next-best session
    /// </summary>
    const string RemoveAndSyncScript = $$"""    
        -- Script Name {{RemoveAndSyncScriptName}}
        local userMappingKey = KEYS[1]
        local userExpiryKey = KEYS[2]
        local fullAuthPrefix = KEYS[3]
        local targetSessionId = ARGV[1]
        local now = tonumber(ARGV[2])

        -- Fetch the specific target session
        local targetJson = redis.call('HGET', userMappingKey, targetSessionId)
        if not targetJson then return 0 end

        local targetDto = cjson.decode(targetJson)
        local targetCorrelationId = targetDto.correlationId

        -- Identify keys to remove
        local keysToRemove = { targetSessionId }

        if targetCorrelationId and targetCorrelationId ~= "" then
            local allFields = redis.call('HGETALL', userMappingKey)
            for i = 1, #allFields, 2 do
                local sId = allFields[i]
                if sId ~= targetSessionId then
                    local sDto = cjson.decode(allFields[i+1])
                    if sDto.correlationId == targetCorrelationId then
                        table.insert(keysToRemove, sId)
                    end
                end
            end
        end

        -- Batch Remove for efficiency
        for _, sId in ipairs(keysToRemove) do
            -- UNLINK is non-blocking and faster for the ticket blobs    
            redis.call('UNLINK', fullAuthPrefix .. sId)
            redis.call('HDEL', userMappingKey, sId)
            redis.call('ZREM', userExpiryKey, sId)
        end

        -- Recalculate Group TTL
        local latest = redis.call('ZREVRANGE', userExpiryKey, 0, 0, 'WITHSCORES')
        if #latest > 0 then           
            local latestScore = tonumber(latest[2])
            local ttl = latestScore - now
            if ttl > 0 then
                redis.call('EXPIRE', userMappingKey, ttl)
                redis.call('EXPIRE', userExpiryKey, ttl)
            end
        else
            -- Clean up index keys entirely if no sessions remain
            redis.call('UNLINK', userMappingKey, userExpiryKey)
        end

        return #keysToRemove
        """;

    /// <summary>
    /// This script is used to efficiently remove all sessions for a user. It retrieves all session IDs from the Hash, deletes each corresponding ticket from the Distributed Cache, and then removes the Hash and Sorted Set entries for that user. By doing this in a single Lua script, we minimize the number of round-trips to Redis and ensure atomicity of the operation.
    /// </summary>   
    const string RemoveAllSessionsScript = $$"""  
        -- Script Name {{RemoveAllSessionsScriptName}}
        local userMappingKey = KEYS[1]
        local userExpiryKey = KEYS[2]
        local fullPrefixedPrefix = KEYS[3]

        -- Get all session ids for the user
        local sessionIds = redis.call('HKEYS', userMappingKey)

        if #sessionIds > 0 then
            local authKeys = {}
            for _, id in ipairs(sessionIds) do
                -- Use the prefixed prefix from KEYS[3]
                table.insert(authKeys, fullPrefixedPrefix .. id)

                -- Batch UNLINK every 100 keys
                if #authKeys >= 100 then
                    redis.call('UNLINK', unpack(authKeys))
                    authKeys = {} 
                end
            end

            if #authKeys > 0 then
                redis.call('UNLINK', unpack(authKeys))
            end
        end

        -- Wipe the index keys themselves
        redis.call('UNLINK', userMappingKey, userExpiryKey)

        return #sessionIds
        """;

    /// <summary>
    /// Script to Remove all sessions that do not match the specified correlation id and update the group TTL to the next-best session
    /// </summary>
    const string RemoveUnrelatedAndSyncScript = $$"""  
        -- Script Name {{RemoveUnrelatedAndSyncScriptName}}
        local userMappingKey = KEYS[1]
        local userExpiryKey = KEYS[2]
        local fullAuthPrefix = KEYS[3]
        local correlationId = ARGV[1]
        local now = tonumber(ARGV[2])

        -- Identify keys to remove, all sessions that do NOT match the correlation id (i.e., unrelated sessions) will be removed
        local keysToRemove = {}

        local allFields = redis.call('HGETALL', userMappingKey)    
        for i = 1, #allFields, 2 do   
            local sId = allFields[i]
            local sDto = cjson.decode(allFields[i+1])
            if sDto.correlationId ~= correlationId then            
                table.insert(keysToRemove, sId)
            end
        end
       
        -- Batch Remove for efficiency
        for _, sId in ipairs(keysToRemove) do
           -- UNLINK is non-blocking and faster for the ticket blobs    
           redis.call('UNLINK', fullAuthPrefix .. sId)
           redis.call('HDEL', userMappingKey, sId)
           redis.call('ZREM', userExpiryKey, sId)
        end

        -- Recalculate Group TTL
        local latest = redis.call('ZREVRANGE', userExpiryKey, 0, 0, 'WITHSCORES')
        if #latest > 0 then           
            local latestScore = tonumber(latest[2])
            local ttl = latestScore - now
            if ttl > 0 then
                redis.call('EXPIRE', userMappingKey, ttl)
                redis.call('EXPIRE', userExpiryKey, ttl)
            end
        else
        -- Clean up index keys entirely if no sessions remain
        redis.call('UNLINK', userMappingKey, userExpiryKey)
        end

        return #keysToRemove
     """;

    public async Task<string> StoreAsync(AuthenticationTicket authenticationTicket)
    {
        var now = _timeProvider.GetUtcNow();
        authenticationTicket.Properties.ExpiresUtc ??= now.Add(_securityOptions.CurrentValue.CookieTimeout);
        
        return await Store(
            authenticationTicket, 
            now);
    }
       
    public string Protect(AuthenticationTicket authenticationTicket)
        => Protect(authenticationTicket, null);

    public string Protect(
       AuthenticationTicket authenticationTicket,
       string? purpose)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext!.Items[SessionCorrelationKey] is not string correlationId)
        {
            correlationId = Guid.NewGuid().ToString();
            httpContext!.Items[SessionCorrelationKey] = correlationId;
        }
        
        var now = _timeProvider.GetUtcNow();

        // Returns the sessionId as bearer token
        return Task.Run(() => Store(
            authenticationTicket,
            now,
            correlationId)).GetAwaiter().GetResult();
    }

    public AuthenticationTicket? Unprotect(string? sessionId)
        => Unprotect(sessionId, null);

    public AuthenticationTicket? Unprotect(
        string? sessionId,
        string? purpose)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) { return null; }

        return Task.Run(() => RetrieveAsync(sessionId)).GetAwaiter().GetResult();
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string sessionId)
    {
        var parts = sessionId.Split(':');
        var userId = parts[0]; 
        var value = await _database.StringGetAsync($"{{User:{userId}}}:{KeyPrefix}{sessionId.Hash()}");

        var authenticationTicket = value.IsNull ? null : TicketSerializer.Default.Deserialize(value!);
        if (authenticationTicket is null) { return null; }

        await UpdateUserSession(
            sessionId,
            authenticationTicket,
            userId,
           _timeProvider.GetUtcNow());

        // Update the session's metadata (last seen) in Redis
        return authenticationTicket;
    }

    public async Task RenewAsync(
        string sessionId,
        AuthenticationTicket authenticationTicket)
    {
        var userId = authenticationTicket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = _timeProvider.GetUtcNow();
        authenticationTicket.Properties.ExpiresUtc ??= now.Add(_securityOptions.CurrentValue.CookieTimeout);
         
        // Update the session's expiry and metadata (last seen) in Redis
        await UpdateUserSession(
            sessionId, 
            authenticationTicket, 
            userId!,
            now);
    }

    public async Task<IEnumerable<UserSessionDto>> GetAll(long userId)
    {
        // Prune expired sessions and return active ones in 1 round-trip
        var result = await _database.ScriptEvaluateAsync(
            GetAndPruneScript,
            [               
                $"{{User:{userId}}}:{UserSessionPrefix}", 
                $"{{User:{userId}}}:{UserExpiryPrefix}",
                $"{{User:{userId}}}:{KeyPrefix}"
            ],
            [
                _timeProvider.GetUtcNow().ToUnixTimeSeconds()
            ]);

        var redisValues = (RedisValue[]?)result;
        if (redisValues is null) { return []; }

        // Treat tickets with the same correlation id (Access/Refresh token) as one,
        //   pick the most recently used amongst them.
        // Deleting any of them will cause the remaining to be deleted.
        return redisValues
            .Select(v => JsonSerializer.Deserialize<UserSessionDto>((string)v!, _jsonOptions)!)
            .Where(v => v is not null)
            .OrderByDescending(v => v.LastSeen)
            .DistinctBy(v => string.IsNullOrWhiteSpace(v.CorrelationId) ? Guid.NewGuid().ToString() : v.CorrelationId);
    }

    public async Task RemoveAll(long userId)
    {
        // Execute the script atomically on the Redis server
        await _database.ScriptEvaluateAsync(
           RemoveAllSessionsScript,
           [
               $"{{User:{userId}}}:{UserSessionPrefix}", // Key 1: Mapping Index
               $"{{User:{userId}}}:{UserExpiryPrefix}",  // Key 2: Expiry Index
               $"{{User:{userId}}}:{KeyPrefix}" // Key 3: Session Index
           ],
           []
        );
    }

    public async Task RemoveAsync(string sessionId)
    {
        var parts = sessionId.Split(':');
        var userId = parts[0];

        var sessionIdHash = sessionId.Hash();
        var sessionKey = $"{{User:{userId}}}:{KeyPrefix}{sessionIdHash}";
        var ticketBytes = await _database.StringGetAsync(sessionKey);
        if (ticketBytes.IsNull) { return; }

        var ticket = TicketSerializer.Default.Deserialize(ticketBytes!);

        await Remove(
            sessionIdHash,
            ticket);
    }

    public async Task Remove(
        long userId, 
        string sessionIdHash)
    {
        var ticketBytes = await _database.StringGetAsync($"{{User:{userId}}}:{KeyPrefix}{sessionIdHash}");
        if (ticketBytes.IsNull) { return; }

        var ticket = TicketSerializer.Default.Deserialize(ticketBytes!);

        var ticketUserId = ticket?.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        // Guard against cross-user session deletion
        if (ticketUserId != userId.ToString()) { return; }

        await Remove(
            sessionIdHash,
            ticket);
    }

    private async Task<string> Store(
        AuthenticationTicket authenticationTicket,
        DateTimeOffset now,
        string? correlationId = null)
    {
        var userId = authenticationTicket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        if (_securityOptions.CurrentValue.LoginConcurrencyMode != LoginConcurrencyMode.AllowMultiple)
        {
            await EnforceLoginPolicy(
                userId!,
                correlationId,
                now);
        }

        var dto = CreateUserSession(
            sessionId,
            authenticationTicket,
            now,
            correlationId);

        await StoreAndSync(
            userId!,
            dto,
            authenticationTicket,
            now);

        return sessionId;
    }

    private async Task EnforceLoginPolicy(
        string userId,
        string? correlationId,
        DateTimeOffset now)
    {
        if (_securityOptions.CurrentValue.LoginConcurrencyMode == LoginConcurrencyMode.Block)
        {
            var result = await _database.ScriptEvaluateAsync(
                GetAndPruneScript, // This ensures the active session count is 100% accurate.   
                [
                    $"{{User:{userId}}}:{UserSessionPrefix}",
                    $"{{User:{userId}}}:{UserExpiryPrefix}",
                    $"{{User:{userId}}}:{KeyPrefix}" // Prefixed automatically!
                ],
                [
                    now.ToUnixTimeSeconds()
                ]);

            if (result.Length == 0) { return; }

            var sessions = ((RedisValue[])result!).Where(v => !v.IsNull);

            var hasConflict = string.IsNullOrWhiteSpace(correlationId)
                || sessions.Any(v => JsonSerializer.Deserialize<UserSessionDto>((string)v!, _jsonOptions)?.CorrelationId != correlationId);

            if (hasConflict)
            {
                throw new DomainException(Constants.ConcurrentLoginErrorMessage);
            }
        }

        // Remove existing sessions for kick out
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            await RemoveAll(long.Parse(userId!));
        }
        else
        {
            await RemoveUnrelated(
                correlationId,
                long.Parse(userId!));
        }
    }

    private async Task UpdateUserSession(
        string sessionId,
        AuthenticationTicket authenticationTicket,
        string userId,
        DateTimeOffset now)
    {
        var existing = await _database.HashGetAsync(
            $"{{User:{userId}}}:{UserSessionPrefix}",
            sessionId.Hash());

        // Reconstruct if missing from cache for some reason
        var dto = existing != RedisValue.Null ?
            JsonSerializer.Deserialize<UserSessionDto>((string)existing!, _jsonOptions)
            : CreateUserSession(
                sessionId,
                authenticationTicket,
                now);

        dto!.LastSeen = now;
        // Other updates here if needed

        await StoreAndSync(
           userId!,
           dto,
           authenticationTicket,
           now);
    }

    private UserSessionDto CreateUserSession(
        string sessionId,
        AuthenticationTicket authenticationTicket,
        DateTimeOffset now,
        string? correlationId = null)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return new UserSessionDto
        {
            AuthScheme = authenticationTicket.AuthenticationScheme,
            CorrelationId = correlationId,
            DeviceInfo = GetDeviceInfo(httpContext?.Request.Headers.UserAgent ?? string.Empty),
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            LastSeen = now,
            Location = GetLocation(httpContext?.Connection.RemoteIpAddress?.ToString()),
            LoginTime = now,
            SessionIdHash = sessionId.Hash() // Store only the hash of the sessionId for privacy
        };
    }

    private async Task StoreAndSync(
        string userId,
        UserSessionDto userSessionDto,
        AuthenticationTicket ticket,
        DateTimeOffset now)
    {
        var serializedTicket = TicketSerializer.Default.Serialize(ticket);
        var userSessionJson = JsonSerializer.Serialize(userSessionDto, _jsonOptions);

        await _database.ScriptEvaluateAsync(
            StoreAndSyncScript,
            keys:
            [
                // $"{{User:{userId}}}: co-locates the session blob with the user's other keys
                // This allows Lua script to perform UNLINK, HDEL, and ZREM across these different keys
                // without triggering a CROSSSLOT error in  clustered Redis environments.
                $"{{User:{userId}}}:{KeyPrefix}{userSessionDto.SessionIdHash}",
                $"{{User:{userId}}}:{UserSessionPrefix}",
                $"{{User:{userId}}}:{UserExpiryPrefix}"
            ],
            values:
            [
                userSessionDto.SessionIdHash,
                ticket.Properties.ExpiresUtc!.Value.ToUnixTimeSeconds(),
                now.ToUnixTimeSeconds(),
                userSessionJson,
                serializedTicket
            ]);
    }

    private string GetLocation(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return GenericLocation;
        }

        try
        {
            // Attempt MaxMind Lookup
            if (!_geoReader.TryCity(ipAddress, out var response)) { return GenericLocation; }

            var city = response!.City?.Name;
            var country = response.Country?.Name;

            // Return "City, Country", or just "Country", or fallback
            if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(country))
            {
                return $"{city}, {country}";
            }

            return country ?? GenericLocation;
        }
        catch
        {
            // In case of any lookup/parsing errors, return generic info
            return GenericLocation;          
        }
    }

    private string GetDeviceInfo(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "Unknown Device";
        }

        var genericInfo = "Generic Device";

        try
        {
            // Parse returns "Other" if unrecognized
            var client = _uaParser.Parse(userAgent);
            var browser = client.UA?.Family;
            var os = client.OS?.Family;

            // Build a friendly string: "Chrome on Windows"
            if (!string.IsNullOrWhiteSpace(browser) 
                && !string.IsNullOrWhiteSpace(os))
            {
                return $"{browser} on {os}";
            }

            return browser ?? os ?? genericInfo;
        }
        catch
        {
            return genericInfo;
        }
    }
   
    private async Task Remove(
        string sessionIdHash, 
        AuthenticationTicket? ticket)
    {
        if (ticket is null) { return; }

        var userId = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        // Remove from Hash/ZSET and sync group TTL
        await _database.ScriptEvaluateAsync(
            RemoveAndSyncScript,
            [
                $"{{User:{userId}}}:{UserSessionPrefix}", // Hash Tag ensures same slot in clustered Redis
                $"{{User:{userId}}}:{UserExpiryPrefix}", 
                $"{{User:{userId}}}:{KeyPrefix}" // Prefix as KEY (library prepends namespace)
            ],
            [
                sessionIdHash,
                _timeProvider.GetUtcNow().ToUnixTimeSeconds()
            ]
        );
    }

    private async Task RemoveUnrelated(
        string correlationId,
        long userId)
    {
        await _database.ScriptEvaluateAsync(
            RemoveUnrelatedAndSyncScript,
            [
                $"{{User:{userId}}}:{UserSessionPrefix}",
                $"{{User:{userId}}}:{UserExpiryPrefix}",
                $"{{User:{userId}}}:{KeyPrefix}"
            ],
            [
                correlationId,
                _timeProvider.GetUtcNow().ToUnixTimeSeconds()
            ]
        );
    }
}

public interface IUserSessionManager
{
    Task<IEnumerable<UserSessionDto>> GetAll(long userId);

    /// <summary>
    /// Removes a specific session from the store using its hashed identifier.
    /// </summary>
    /// <param name="userId">The unique identifier of the user who owns the session.</param>
    /// <param name="sessionIdHash">The URL-safe Base64 encoded SHA256 hash of the original session ID.</param>
    /// <remarks>
    /// This method is intended for administrative or manual session management. 
    /// It is not invoked by standard .NET Identity or authentication middleware.
    /// For security, the raw session ID is never persisted; only this hash is stored and visible in the backend.
    /// </remarks>
    Task Remove(
        long userId,
        string sessionIdHash);

    Task RemoveAll(long userId);
}

public record UserSessionDto
{
    public string? AuthScheme { get; set; }

    /// <summary>
    /// All sessions (Access + Refresh tokens) initiated in the same login flow will share the same CorrelationId 
    ///   and be treated as one session. Deleting any of them will cause the rest to be deleted as well.
    /// </summary>
    public string? CorrelationId { get; set; }

    public string DeviceInfo { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public DateTimeOffset LastSeen { get; set; }

    public string Location { get; set; } = string.Empty;

    public DateTimeOffset LoginTime { get; set; }

    /// <summary>
    /// Gets or sets the hashed version of the session identifier.
    /// </summary>
    /// <value>A URL-safe Base64 encoded SHA256 hash.</value>
    /// <remarks>
    /// For security, the raw session ID is never persisted in Redis. This hashed 
    /// representation is used for storage lookups and session management to 
    /// prevent session hijacking in the event of a database compromise.
    /// </remarks>
    public string SessionIdHash { get; set; } = string.Empty;
}
