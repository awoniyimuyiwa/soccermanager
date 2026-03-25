using Api.Attributes;
using Api.Filters;
using Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api.UnitTests;

public class IdempotencyFilterTests
{
    private static readonly string IdempotencyKey = Guid.NewGuid().ToString();
    private const string UserId = "user-123";
    private static readonly string LockKey = $"{IdempotencyFilter.CachePrefix}{IdempotencyFilter.LockPrefix}{UserId}:{IdempotencyKey}";
    private static readonly string RecordKey = $"{IdempotencyFilter.CachePrefix}{IdempotencyFilter.RecordPrefix}{UserId}:{IdempotencyKey}";
   
    [Fact]
    public async Task InvokeAsync_NoAttribute_ReturnsNext()
    {
        // Arrange
        var (context, 
            cache, 
            db, 
            options) = GetSetup();
        
        var filter = new IdempotencyFilter(
            cache.Object, 
            db.Object, 
            options);
        var nextCalled = false;

        // Act
        await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        });

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_MissingHeader_ReturnsBadRequest()
    {
        // Arrange
        var (context, 
            cache, 
            db, 
            options) = GetSetup(hc => hc.SetEndpoint(CreateEndpoint()));
        
        var filter = new IdempotencyFilter(
            cache.Object, 
            db.Object, 
            options);

        // Act
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(null));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            IdempotencyFilter.MissingHeaderErrorMessage, 
            badRequest.Value!);
    }

    [Fact]
    public async Task InvokeAsync_UserNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var (context,
            cache, 
            db, 
            options) = GetSetup(hc => 
            {
                hc.SetEndpoint(CreateEndpoint());
                hc.Request.Headers[Constants.IdempotencyKeyHeaderName] = IdempotencyKey;
                // No User set = Unauthenticated
            });

        var filter = new IdempotencyFilter(
            cache.Object, 
            db.Object, 
            options);

        // Act
        var result = await filter.InvokeAsync(
            context, 
            _ => ValueTask.FromResult<object?>(null));

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task InvokeAsync_CacheHitWithCorrectHash_ReturnsCachedResult()
    {
        // Arrange
        var (context, 
            cache, 
            db, 
            options) = GetSetup(hc => 
            {
                hc.SetEndpoint(CreateEndpoint());
                hc.Request.Headers[Constants.IdempotencyKeyHeaderName] = IdempotencyKey;
                hc.User = CreateUser();
            });

        var body = "{\"data\":\"test\"}";
        var requestHash = Convert.ToBase64String(SHA256.HashData("{}"u8.ToArray()));
       
        var record = new IdempotencyRecord(
            body, 
            requestHash, 
            200,
            []);
        var serialized = JsonSerializer.Serialize(record);

        cache.Setup(c => c.GetAsync(
            RecordKey,
            default))
             .ReturnsAsync(Encoding.UTF8.GetBytes(serialized));

        var filter = new IdempotencyFilter(
            cache.Object, 
            db.Object, 
            options);

        // Act
        var result = await filter.InvokeAsync(
            context, 
            _ => ValueTask.FromResult<object?>(null));

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(200, contentResult.StatusCode);
        Assert.Equal(body, contentResult.Content);
        Assert.True(context.HttpContext.Response.Headers.ContainsKey(IdempotencyFilter.IdempotentReplayedHeaderName));
    }

    [Fact]
    public async Task InvokeAsync_CacheHitWithDifferentHash_ReturnsBadRequest()
    {
        // Arrange
        var (context,
            cache,
            db,
            options) = GetSetup(hc =>
            {
                hc.SetEndpoint(CreateEndpoint());
                hc.Request.Headers[Constants.IdempotencyKeyHeaderName] = IdempotencyKey;
                hc.User = CreateUser();
            });

        var record = new IdempotencyRecord(
            "{}",
            "wrong-hash",
            200, []);

        cache.Setup(c => c.GetAsync(
            RecordKey,
            default))
             .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record)));

        var filter = new IdempotencyFilter(
            cache.Object,
            db.Object,
            options);

        // Act
        var result = await filter.InvokeAsync(
            context,
            _ => ValueTask.FromResult<object?>(null));

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            IdempotencyFilter.ReusedErrorMessage,
            badRequest.Value!);
    }

    [Fact]
    public async Task InvokeAsync_LockAcquisitionFails_ReturnsConflict()
    {
        // Arrange
        var (context, 
            cache, 
            db, 
            options) = GetSetup(hc => 
            {
                hc.SetEndpoint(CreateEndpoint());
                hc.Request.Headers[Constants.IdempotencyKeyHeaderName] = IdempotencyKey;
                hc.User = CreateUser();
            });

        db.Setup(d => d.LockTakeAsync(
            LockKey, 
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan>(), 
            CommandFlags.None))
          .ReturnsAsync(false);

        var filter = new IdempotencyFilter(
            cache.Object, 
            db.Object, 
            options);

        // Act
        var result = await filter.InvokeAsync(
            context, 
            _ => ValueTask.FromResult<object?>(null));

        // Assert
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(
            IdempotencyFilter.ConflictErrorMessage, 
            conflict.Value);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ValidRequest_ProceedsAndCaches()
    {
        // Arrange
        var (context,
            cache,
            db,
            options) = GetSetup(hc =>
            {
                hc.SetEndpoint(CreateEndpoint());
                hc.Request.Headers[Constants.IdempotencyKeyHeaderName] = IdempotencyKey;
                hc.User = CreateUser();
            });

        db.Setup(d => d.LockTakeAsync(
            LockKey,
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan>(),
            CommandFlags.None))
          .ReturnsAsync(true);

        var filter = new IdempotencyFilter(
            cache.Object,
            db.Object,
            options);

        var actionContext = new ActionContext(
            context.HttpContext,
            new RouteData(),
            new ActionDescriptor());
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());

        var actionResult = new ObjectResult(new { success = true })
        {
            StatusCode = 200
        };
        var executedContext = new ActionExecutedContext(
            actionContext,
            [],
            new object())
        {
            Result = actionResult
        };

        // Act
        await filter.OnActionExecutionAsync(
            executingContext,
            () => Task.FromResult(executedContext));

        // Assert
        db.Verify(d => d.LockReleaseAsync(
            LockKey,
            It.IsAny<RedisValue>(),
            CommandFlags.None),
            Times.Once);

        cache.Verify(c => c.SetAsync(
            RecordKey,
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            default),
            Times.Once);
    }

    private static (
        DefaultEndpointFilterInvocationContext Context, 
        Mock<IDistributedCache> Cache, Mock<IDatabase> Db, 
        IOptions<IdempotencyOptions> Options) GetSetup(
        Action<HttpContext>? setupAction = null)
    {
        var httpContext = new DefaultHttpContext();

        // Setup a valid empty JSON body to prevent JsonException during hashing
        var jsonBody = "{}"u8.ToArray();
        httpContext.Request.Body = new MemoryStream(jsonBody);
        httpContext.Request.ContentLength = jsonBody.Length;
        httpContext.Request.ContentType = "application/json";

        httpContext.Features.Set<IHttpResponseBodyFeature>(
            new StreamResponseBodyFeature(new MemoryStream()));

        setupAction?.Invoke(httpContext);

        var cache = new Mock<IDistributedCache>();

        // Explicitly return null for GetAsync to avoid "empty string" deserialization errors
        cache.Setup(c => c.GetAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var db = new Mock<IDatabase>();
        var options = Microsoft.Extensions.Options.Options.Create(new IdempotencyOptions());

        return (new DefaultEndpointFilterInvocationContext(httpContext), 
            cache, 
            db, 
            options);
    }

    private static ClaimsPrincipal CreateUser() =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId)], 
            "Test"));

    private static Endpoint CreateEndpoint() =>
        new(_ => Task.CompletedTask,
            new EndpointMetadataCollection([new IdempotentAttribute()]), 
            "Test");
}
