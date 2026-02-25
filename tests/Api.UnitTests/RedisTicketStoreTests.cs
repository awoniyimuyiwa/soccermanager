using Api.Options;
using Domain;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using Moq;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using UAParser;

namespace Api.UnitTests;

public class RedisTicketStoreTests
{
    static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    [Fact]
    public async Task StoreAsync_MultipleMode_StoresSession()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.AllowMultiple);

        var (redisTicketStore,
           redisDatabaseMock,
           timeProvider) = CreateTicketStore(
               securityOptions,
               new Mock<IHttpContextAccessor>().Object);
       
        // Act
        var sessionId = await redisTicketStore.StoreAsync(authTicket);

        // Assert
        // No session pruning 
        redisDatabaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Never);

        AssertSessionStored(
            redisDatabaseMock,
            authTicket, 
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionId = sessionId
            });
    }

    [Fact]
    public async Task StoreAsync_KickOutMode_ClearsPreviousSessionsAndStoresCurrent()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);
        
        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.KickOut);

        var (redisTicketStore,
          redisDatabaseMock,
          timeProvider) = CreateTicketStore(
              securityOptions,
              new Mock<IHttpContextAccessor>().Object);

        SetupExistingSessions(redisDatabaseMock);

        // Act
        var sessionId = await redisTicketStore.StoreAsync(authTicket);

        // Assert
        // Kickout
        redisDatabaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAllSessionsScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                         && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                         && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
                It.Is<RedisValue[]>(v => v.IsNullOrEmpty()),
                It.IsAny<CommandFlags>()),
            Times.Once);

        AssertSessionStored(
            redisDatabaseMock, 
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionId = sessionId
            });
    }

    [Fact]
    public async Task StoreAsync_BlockModeWithConflict_ThrowsException()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.Block);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore(
                securityOptions, 
                new Mock<IHttpContextAccessor>().Object);

        SetupExistingSessions(redisDatabaseMock);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => redisTicketStore.StoreAsync(authTicket));
        Assert.Equal(
            Constants.ConcurrentLoginErrorMessage,
            exception.Message);

        AssertSessionPruning(
            redisDatabaseMock,
            timeProvider,
            userId);

        // Nothing deleted
        redisDatabaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public void Protect_MultipleMode_StoresSession()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.AllowMultiple);

        var httpContext = new DefaultHttpContext();
        //httpContext.Request.Headers.UserAgent = "Mozilla/5.0...";
        //httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var (redisTicketStore,
           redisDatabaseMock,
           timeProvider) = CreateTicketStore(
               securityOptions,
               httpContextAccessorMock.Object);

        // Act
        var sessionId = redisTicketStore.Protect(authTicket);

        // Assert
        // No session pruning 
        redisDatabaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Never);

        AssertSessionStored(
            redisDatabaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                CorrelationId = httpContext.Items[RedisTicketStore.SessionCorrelationKey]!.ToString(),
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionId = sessionId
            });
    }

    [Fact]
    public void Protect_BlockModeWithConflict_ThrowsException()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.Block);

        var httpContext = new DefaultHttpContext();

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore(
               securityOptions,
               httpContextAccessorMock.Object);

        SetupExistingSessions(redisDatabaseMock);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => redisTicketStore.Protect(authTicket));
        Assert.Equal(
            Constants.ConcurrentLoginErrorMessage,
            exception.Message);

        // Assert
        AssertSessionPruning(
            redisDatabaseMock,
            timeProvider,
            userId);

        // Nothing deleted
        redisDatabaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public void Protect_BlockModeWithNoConflict_StoresSession()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.Block);

        // Simulate a session with a matching correlation ID already in the store
        var correlationId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[RedisTicketStore.SessionCorrelationKey] = correlationId;
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore(
               securityOptions,
               httpContextAccessorMock.Object);

        SetupExistingSessions(
            redisDatabaseMock, 
            correlationId);

        // Act
        var sessionId = redisTicketStore.Protect(authTicket);

        // Assert
        AssertSessionPruning(
            redisDatabaseMock,
            timeProvider,
            userId);

        AssertSessionStored(
            redisDatabaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                CorrelationId = httpContext.Items[RedisTicketStore.SessionCorrelationKey]!.ToString(),
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionId = sessionId
            });
    }

    [Fact]
    public void Protect_KickOutMode__ClearsOnlyUnrelatedSessionsAndStoresCurrent()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.KickOut);

        // Simulate a session with a matching correlation ID already in the store
        var correlationId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[RedisTicketStore.SessionCorrelationKey] = correlationId;
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore(
               securityOptions,
               httpContextAccessorMock.Object);

        SetupExistingSessions(
            redisDatabaseMock,
            correlationId);

        // Act
        var sessionId = redisTicketStore.Protect(authTicket);

        // Assert
        // Deleting only unrelated sessions
        redisDatabaseMock.Verify(
          db => db.ScriptEvaluateAsync(
              It.Is<string>(s => s.Contains(RedisTicketStore.RemoveUnrelatedAndSyncScriptName)),
              It.Is<RedisKey[]>(
                  k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                       && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                       && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
              It.Is<RedisValue[]>(
                  v => v[0] == correlationId
                        && v[1] == timeProvider.GetUtcNow().ToUnixTimeSeconds()),
              It.IsAny<CommandFlags>()),
          Times.Once);

        AssertSessionStored(
            redisDatabaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                CorrelationId = httpContext.Items[RedisTicketStore.SessionCorrelationKey]!.ToString(),
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionId = sessionId
            });
    }

    [Fact]
    public async Task GetAll_ReturnsValidResult()
    {
        // Arrange
        var userId = new Random().Next();

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore();

        SetupExistingSessions(redisDatabaseMock);

        // Act
        var result = await redisTicketStore.GetAll(userId);

        // Assert
        AssertSessionPruning(
            redisDatabaseMock,
            timeProvider,
            userId);

        Assert.NotNull(result);
        Assert.True(result.Any());
        Assert.True(result.DistinctBy(u => u.CorrelationId).Count() == result.Count());
    }

    [Fact]
    public async Task RetrieveAsync_SessionExists_ReturnsSession()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            _) = CreateTicketStore();

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        redisDatabaseMock.Setup(db => db.StringGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionId}",
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TicketSerializer.Default.Serialize(authTicket));

        // Act
        var actual = await redisTicketStore.RetrieveAsync(sessionId);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(
            userId.ToString(), 
            authTicket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task RetrieveAsync_SessionDoesNotExist_ReturnsNull()
    {
        // Arrange
        var (redisTicketStore,
            _,
            _) = CreateTicketStore();

        // Act
        var actual = await redisTicketStore.RetrieveAsync(Guid.NewGuid().ToString());

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void Unprotect_SessionExists_ReturnsSession()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            _) = CreateTicketStore(
                null,
                new Mock<IHttpContextAccessor>().Object);

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        redisDatabaseMock.Setup(db => db.StringGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionId}",
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TicketSerializer.Default.Serialize(authTicket));

        // Act
        var actual = redisTicketStore.Unprotect(sessionId);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(
            userId.ToString(),
            authTicket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task RenewAsync_SessionExists_IsSuccessful()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore(
                null,
                new Mock<IHttpContextAccessor>().Object);

        // Simulate existing session
        var userSessionDto = new UserSessionDto()
        {
            AuthScheme = Guid.NewGuid().ToString(),
            DeviceInfo = Guid.NewGuid().ToString(),
            Location = Guid.NewGuid().ToString(),
            LoginTime = timeProvider.GetUtcNow().AddDays(-1 *random.Next(short.MaxValue)),
            SessionId = $"{userId}:{Guid.NewGuid()}"
        };
        redisDatabaseMock.Setup(db => db.HashGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}",
            userSessionDto.SessionId,
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(JsonSerializer.Serialize(userSessionDto, _jsonOptions));

        // Act
        await redisTicketStore.RenewAsync(
            userSessionDto.SessionId, 
            authTicket);

        // Assert
        AssertSessionStored(
            redisDatabaseMock,
            authTicket,
            userSessionDto with
            {
                LastSeen = timeProvider.GetUtcNow()
            });
    }

    [Fact]
    public async Task RenewAsync_SessionDoesNotExist_ReconstructsSession()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore(
                null, 
                new Mock<IHttpContextAccessor>().Object);
        
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        
        // Act
        await redisTicketStore.RenewAsync(
            sessionId,
            authTicket);

        // Assert
        AssertSessionStored(
            redisDatabaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionId = sessionId
            });
    }

    [Fact]
    public async Task RemoveAsync_SessionExists_IsSuccessful()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock, 
            timeProvider) = CreateTicketStore();

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        redisDatabaseMock.Setup(db => db.StringGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionId}",
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TicketSerializer.Default.Serialize(authTicket));

        // Act
        await redisTicketStore.RemoveAsync(sessionId);

        // Assert
        AssertSessionRemoved(
            redisDatabaseMock,
            timeProvider,
            userId,
            sessionId);
    }

    [Fact]
    public async Task RemoveAsync_SessionDoesntExist_IsIdempotent()
    {
        // Arrange
        var (redisTicketStore,
            redisDatabaseMock,
            _) = CreateTicketStore();

        // Simulate non existing session
        var sessionId = Guid.NewGuid().ToString();
       
        // Act
        await redisTicketStore.RemoveAsync(sessionId);

        // Assert
        redisDatabaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task Remove_SessionExists_IsSuccessful()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            timeProvider) = CreateTicketStore();

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        redisDatabaseMock.Setup(db => db.StringGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionId}",
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TicketSerializer.Default.Serialize(authTicket));

        // Act
        await redisTicketStore.Remove(
            userId,
            sessionId);

        // Assert
        AssertSessionRemoved(
            redisDatabaseMock,
            timeProvider,
            userId, 
            sessionId);
    }

    [Fact]
    public async Task Remove_UserIdDoesNotMatchTheOneOnSession_DoesNotRemove()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            _) = CreateTicketStore();

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        redisDatabaseMock.Setup(db => db.StringGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionId}",
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TicketSerializer.Default.Serialize(authTicket));

        var wrongUserId = userId + random.Next(1, int.MaxValue);

        // Act
        await redisTicketStore.Remove(    
            wrongUserId,    
            sessionId);

        // Assert
        redisDatabaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task Remove_SessionDoesNotExist_IsIdempotent()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var (redisTicketStore,
            redisDatabaseMock,
            _) = CreateTicketStore();

        // Simulate non existing session
        var sessionId = Guid.NewGuid().ToString();

        // Act
        await redisTicketStore.Remove(
            userId,
            sessionId);

        // Assert
        redisDatabaseMock.Verify(
            db => db.KeyDeleteAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveAll_IsSuccessful()
    {
        // Arrange
        var userId = new Random().Next();

        var (redisTicketStore,
            redisDatabaseMock,
            _) = CreateTicketStore();

        // Act
        await redisTicketStore.RemoveAll(userId);

        // Assert
        redisDatabaseMock.Verify(
           db => db.ScriptEvaluateAsync(
               It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAllSessionsScriptName)),
               It.Is<RedisKey[]>(
                   k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                        && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                        && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
               It.Is<RedisValue[]>(v => v.IsNullOrEmpty()),
               It.IsAny<CommandFlags>()),
               Times.Once);
    }

    private static (
       RedisTicketStore ticketStore,
       Mock<IDatabase> databaseMock,
       TimeProvider timeProvider) CreateTicketStore(
       IOptionsMonitor<SecurityOptions>? securityOptions = null,
       IHttpContextAccessor? httpContextAccessor = null)
    {
        var redisDatabaseMock = new Mock<IDatabase>();

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            redisDatabaseMock.Object,
            new Mock<IGeoIP2DatabaseReader>().Object,
            httpContextAccessor!,
            securityOptions!,
            timeProvider,
            Parser.GetDefault());

        return (
            store,
            redisDatabaseMock,
            timeProvider);
    }

    private static IOptionsMonitor<SecurityOptions> CreateSecurityOptions(LoginConcurrencyMode mode)
    {
        var securityOptionsMock = new Mock<IOptionsMonitor<SecurityOptions>>();
        securityOptionsMock.Setup(o => o.CurrentValue)
            .Returns(new SecurityOptions
            {
                CookieTimeout = TimeSpan.FromHours(1),
                LoginConcurrencyMode = mode
            });

        return securityOptionsMock.Object;
    }

    private static AuthenticationTicket CreateAuthenticationTicket(long userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationTicket(
            principal,
            new AuthenticationProperties()
            { 
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) 
            },
            "DefaultScheme");
    }

    private static void SetupExistingSessions(
        Mock<IDatabase> redisDatabaseMock,
        string correlationId = "test-correlation")
    {
        var existingSessionCount = new Random().Next(1, 10);
        var existingSessions = Enumerable.Range(0, existingSessionCount)
            .Select(i => (RedisValue)JsonSerializer.Serialize(new UserSessionDto
            {
                SessionId = $"user123:session123",
                CorrelationId = correlationId
            },
            _jsonOptions))
            .ToArray();

        redisDatabaseMock.Setup(
            db => db.ScriptEvaluateAsync( 
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(existingSessions));
    }

    private static void AssertSessionPruning(
       Mock<IDatabase> redisDatabaseMock,
       TimeProvider timeProvider,
       long userId)
    {
        redisDatabaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                         && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                         && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
                It.Is<RedisValue[]>(
                    v => v[0] == timeProvider.GetUtcNow().ToUnixTimeSeconds()),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private static void AssertSessionStored(
        Mock<IDatabase> redisDatabaseMock,
        AuthenticationTicket authenticationTicket,
        UserSessionDto userSessionDto)
    {
        var userId = authenticationTicket.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        redisDatabaseMock.Verify(
           db => db.ScriptEvaluateAsync(
               It.Is<string>(s => s.Contains(RedisTicketStore.StoreAndSyncScriptName)),
               It.Is<RedisKey[]>(
                   k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{userSessionDto.SessionId}"
                        && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                        && k[2] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"),
               It.Is<RedisValue[]>(
                   v => v[0] == userSessionDto.SessionId
                        && v[1] == authenticationTicket.Properties.ExpiresUtc!.Value.ToUnixTimeSeconds()
                        && v[2] == userSessionDto.LastSeen.ToUnixTimeSeconds()
                        && AssertEqual(v[3].ToString(), userSessionDto)
                        && v[4] == TicketSerializer.Default.Serialize(authenticationTicket)),
               It.IsAny<CommandFlags>()),
           Times.Once);
    }

    private static void AssertSessionRemoved(
        Mock<IDatabase> redisDatabaseMock,
        TimeProvider timeProvider,
        long userId,
        string sessionId)
    {
        redisDatabaseMock.Verify(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAndSyncScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                         && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                         && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
                It.Is<RedisValue[]>(
                    v => v[0] == sessionId
                         && v[1] == timeProvider.GetUtcNow().ToUnixTimeSeconds()),
                It.IsAny<CommandFlags>()),
                Times.Once);
    }

    private static bool AssertEqual(
        string actualJson,
        UserSessionDto expected)
    {
        var actual = JsonSerializer.Deserialize<UserSessionDto>(actualJson, _jsonOptions);

        return actual is not null
            && actual.AuthScheme == expected.AuthScheme
            && actual.CorrelationId == expected.CorrelationId
            && actual.DeviceInfo == expected.DeviceInfo
            && actual.IpAddress == expected.IpAddress
            && actual.LastSeen == expected.LastSeen
            && actual.Location == expected.Location
            && actual.LoginTime == expected.LoginTime
            && actual.SessionId == expected.SessionId;
    }
}
