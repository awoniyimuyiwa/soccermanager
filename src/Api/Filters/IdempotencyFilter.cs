using Api.Attributes;
using Api.Options;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using StackExchange.Redis;
using System.Net.Mime;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api.Filters;

public class IdempotencyFilter(
    IDistributedCache cache,
    IDatabase database,
    IOptions<IdempotencyOptions> options) : IAsyncActionFilter, IEndpointFilter
{
    public const string CachePrefix = "idempotency:";
    public const string LockPrefix = "lock:";
    public const string RecordPrefix = "record:";

    public const string IdempotentReplayedHeaderName = "Idempotent-Replayed";
    public const string ConflictErrorMessage = "Request is already being processed.";
    public const string InvalidKeyErrorMessage = "The idempotency key must be a valid GUID.";
    public const string MissingHeaderErrorMessage = $"Missing {Constants.IdempotencyKeyHeaderName} header.";
    public const string ReusedErrorMessage = $"{Constants.IdempotencyKeyHeaderName} reused with different payload.";

    // Interface: IAsyncActionFilter (Controllers)
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, 
        ActionExecutionDelegate next)
    {
        var result = await Execute(
            context.HttpContext,
            async () =>
            {
                // Execute the actual Controller Action
                var executedContext = await next();

                // If an exception happened in the controller, bubble it up.
                if (executedContext.Exception != null && !executedContext.ExceptionHandled)
                {
                    throw executedContext.Exception;
                }

                return executedContext.Result;
            });

        if (result is IActionResult actionResult)
        {
            context.Result = actionResult;
        }
    }

    // Interface: IEndpointFilter (Minimal APIs)
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        return await Execute(
            context.HttpContext, 
            async () => await next(context));
    }

    private async Task<object?> Execute(
        HttpContext httpContext, 
        Func<Task<object?>> next)
    {
        var endpoint = httpContext.GetEndpoint();
        var idempotentAttr = endpoint?.Metadata.GetMetadata<IdempotentAttribute>();
        if (idempotentAttr is null) return await next();

        var request = httpContext.Request;
        if (!request.Body.CanSeek)
        {
            throw new InvalidOperationException(
                "Idempotency validation failed: Request body is not seekable. " +
                "Ensure 'context.Request.EnableBuffering()' is called in a middleware prior to this filter.");
        }

        if (!request.Headers.TryGetValue(Constants.IdempotencyKeyHeaderName, out var key))
            return new BadRequestObjectResult(MissingHeaderErrorMessage);

        if (!Guid.TryParse(key, out _))
        {
            return new BadRequestObjectResult(InvalidKeyErrorMessage);
        }

        // Authenticated User Scoping (Security)
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) return new UnauthorizedResult();

        request.Body.Position = 0; // Reset before hashing

        var cancellationToken = httpContext.RequestAborted;

        // Content Integrity (Hash the request),
        // perform all read-only operations (like hashing the incoming body and checking the cache)
        // before a write operation (like taking a lock).
        var requestHash = request.HasFormContentType
             ? await ComputeMultipartHash(request, cancellationToken)
             : await ComputeSimpleBodyHash(request, cancellationToken);

        request.Body.Position = 0; // Reset after hashing

        // Check for cached result
        var recordKey = $"{CachePrefix}{RecordPrefix}{userId}:{key}";
        var recordJson = await cache.GetStringAsync(recordKey, cancellationToken);

        if (recordJson is not null)
        {
            var record = JsonSerializer.Deserialize<IdempotencyRecord>(recordJson);
            // Tampering protection: Ensure the payload hasn't changed since the first request
            if (record!.RequestHash != requestHash)
                return new BadRequestObjectResult(ReusedErrorMessage);

            // Replay Headers & Body
            foreach (var header in record.Headers)
                httpContext.Response.Headers[header.Key] = header.Value;

            httpContext.Response.Headers.Append(IdempotentReplayedHeaderName, "true");

            return new ContentResult
            {
                Content = record.Body,
                ContentType = record.Headers.GetValueOrDefault(
                    HeaderNames.ContentType,
                    MediaTypeNames.Application.Json),
                StatusCode = record.StatusCode
            };
        }

        // Concurrency Protection (Distributed Lock)
        var lockKey = $"{CachePrefix}{LockPrefix}{userId}:{key}";

        // Unique proof of ownership for this specific request
        var lockToken = Guid.NewGuid().ToString();

        // Match Lock TTL to the [RequestTimeout] attribute if present, else use default
        var lockTtl = endpoint?.Metadata.GetMetadata<RequestTimeoutAttribute>()?.Timeout
            ?? TimeSpan.FromSeconds(options.Value.LockTTLSeconds);

        if (!await database.LockTakeAsync(lockKey, lockToken, lockTtl))
            return new ConflictObjectResult(ConflictErrorMessage);

        try
        {
            var result = await next();

            // Extract status code and value from either IActionResult or IResult
            var (statusCode, value) = result switch
            {
                ObjectResult or => (or.StatusCode, or.Value),
                IStatusCodeHttpResult scr and IValueHttpResult vhr => (scr.StatusCode, vhr.Value),
                IStatusCodeHttpResult scr => (scr.StatusCode, null),
                _ => (null, null)
            };

            // Store Result (Success Paths only)
            if (statusCode is >= 200 and < 300)
            {
                var headers = httpContext.Response.Headers
                    .Where(h => h.Key == HeaderNames.Location
                                || h.Key == HeaderNames.ContentType
                                || h.Key == Constants.TraceIdHeaderName
                                || h.Key.StartsWith("X-"))
                    .ToDictionary(h => h.Key, h => h.Value.ToString());

                var record = new IdempotencyRecord(
                    JsonSerializer.Serialize(value),
                    requestHash,
                    statusCode.Value,
                    headers);

                var recordTtl = idempotentAttr.RecordTTLMinutes > 0
                    ? TimeSpan.FromMinutes(idempotentAttr.RecordTTLMinutes)
                    : TimeSpan.FromMinutes(options.Value.RecordTTLMinutes);

                await cache.SetStringAsync(
                    recordKey, 
                    JsonSerializer.Serialize(record),
                    new DistributedCacheEntryOptions 
                    { 
                        AbsoluteExpirationRelativeToNow = recordTtl
                    }, 
                    cancellationToken);
            }

            return result;
        }
        finally
        {
            try { await database.LockReleaseAsync(lockKey, lockToken); } catch { }
        }
    }

    private static async Task<string> ComputeSimpleBodyHash(
        HttpRequest request, 
        CancellationToken cancellationToken)
    {
        // HashDataAsync reads from CURRENT position to end
        byte[] hashBytes = await SHA256.HashDataAsync(
            request.Body, 
            cancellationToken);

        return Convert.ToBase64String(hashBytes);
    }

    private static async Task<string> ComputeMultipartHash(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var form = request.Form;
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var fKey in form.Keys.Order())
            incrementalHash.AppendData(Encoding.UTF8.GetBytes($"{fKey}:{form[fKey]};"));

        const int maxBytes = 1024 * 1024;
        byte[] buffer = new byte[maxBytes];
        foreach (var file in form.Files)
        {
            incrementalHash.AppendData(Encoding.UTF8.GetBytes($"file:{file.Name};size:{file.Length};"));
            using var stream = file.OpenReadStream();
            int bytesToRead = (int)Math.Min(file.Length, maxBytes);
            if (bytesToRead > 0)
            {
                await stream.ReadExactlyAsync(
                    buffer.AsMemory(0, bytesToRead), 
                    cancellationToken);
                incrementalHash.AppendData(buffer.AsSpan(0, bytesToRead));
            }
        }

        return Convert.ToBase64String(incrementalHash.GetHashAndReset());
    }
}

public record IdempotencyRecord(
    string Body, 
    string RequestHash, 
    int StatusCode, 
    Dictionary<string, string> Headers);
