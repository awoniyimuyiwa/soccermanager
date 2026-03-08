using Api.Extensions;
using Api.Options;
using Api.Services;
using Domain;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
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

        var databaseMock = new Mock<IDatabase>();

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.AllowMultiple);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            databaseMock.Object,
            null!,
            new Mock<IGeoIP2DatabaseReader>().Object,
            new Mock<IHttpContextAccessor>().Object,
            securityOptions,
            timeProvider,
            Parser.GetDefault());

        // Act
        var sessionId = await store.StoreAsync(authTicket);

        // Assert
        // No session pruning 
        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
            Times.Never);

        AssertSessionStored(
            databaseMock,
            authTicket, 
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionIdHash = sessionId.Hash()
            });
    }

    [Fact]
    public async Task StoreAsync_KickOutMode_ClearsPreviousSessionsAndStoresCurrent()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var databaseMock = new Mock<IDatabase>();
        SetupExistingSessions(databaseMock);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.KickOut);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            databaseMock.Object,
            null!,
            new Mock<IGeoIP2DatabaseReader>().Object,
            new Mock<IHttpContextAccessor>().Object,
            securityOptions,
            timeProvider,
            Parser.GetDefault());

        // Act
        var sessionId = await store.StoreAsync(authTicket);

        // Assert
        // Kickout
        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAllScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                         && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                         && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
                It.Is<RedisValue[]>(v => v.IsNullOrEmpty()),
                It.IsAny<CommandFlags>()),
            Times.Once);

        AssertSessionStored(
            databaseMock, 
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionIdHash = sessionId.Hash()
            });
    }

    [Fact]
    public async Task StoreAsync_BlockModeWithConflict_ThrowsException()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var databaseMock = new Mock<IDatabase>();
        SetupExistingSessions(databaseMock);

        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.Block);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            databaseMock.Object,
            null!,
            new Mock<IGeoIP2DatabaseReader>().Object,
            null!,
            securityOptions,
            timeProvider,
            Parser.GetDefault());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() => store.StoreAsync(authTicket));
        Assert.Equal(
            Constants.ConcurrentLoginErrorMessage,
            exception.Message);

        AssertSessionPruning(
            databaseMock,
            timeProvider,
            userId);

        // Nothing deleted
        databaseMock.Verify(
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

        var databaseMock = new Mock<IDatabase>();

        var httpContext = new DefaultHttpContext();
        //httpContext.Request.Headers.UserAgent = "Mozilla/5.0...";
        //httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var protector = new EphemeralDataProtectionProvider().CreateProtector("Test");
        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.AllowMultiple);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            databaseMock.Object,
            protector,
            new Mock<IGeoIP2DatabaseReader>().Object,
            httpContextAccessorMock.Object,
            securityOptions!,
            timeProvider,
            Parser.GetDefault());

        var purpose = Guid.NewGuid().ToString();

        // Act
        var protectedText = store.Protect(
            authTicket, 
            purpose);

        // Assert
        Assert.NotNull(protectedText);

        // No session pruning 
        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),
          Times.Never);

        var unprotectedText = protectedText.Unprotect(
            protector.CreateProtector(RedisTicketStore.SessionPurpose),
            purpose);
        Assert.NotNull(unprotectedText);

        AssertSessionStored(
            databaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                CorrelationId = httpContext.Items[RedisTicketStore.SessionCorrelationKey]!.ToString(),
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionIdHash = unprotectedText.Hash()
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

        var databaseMock = new Mock<IDatabase>();
        SetupExistingSessions(databaseMock);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var ticketStore = new RedisTicketStore(
            databaseMock.Object,
            new Mock<IDataProtector>().Object,
            new Mock<IGeoIP2DatabaseReader>().Object,
            httpContextAccessorMock.Object!,
            securityOptions,
            timeProvider,
            Parser.GetDefault());

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => ticketStore.Protect(authTicket));
        Assert.Equal(
            Constants.ConcurrentLoginErrorMessage,
            exception.Message);

        // Assert
        AssertSessionPruning(
            databaseMock,
            timeProvider,
            userId);

        // Nothing deleted
        databaseMock.Verify(
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

        // Simulate a session with a matching correlation ID already in the store
        var correlationId = Guid.NewGuid().ToString();
        var databaseMock = new Mock<IDatabase>();
        SetupExistingSessions(
            databaseMock,
            correlationId);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[RedisTicketStore.SessionCorrelationKey] = correlationId;
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var protector = new EphemeralDataProtectionProvider().CreateProtector("Test");
        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.Block);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            databaseMock.Object,
            protector,
            new Mock<IGeoIP2DatabaseReader>().Object,
            httpContextAccessorMock.Object,
            securityOptions!,
            timeProvider,
            Parser.GetDefault());

        var purpose = Guid.NewGuid().ToString();

        // Act
        var protectedText = store.Protect(
            authTicket, 
            purpose);

        // Assert
        Assert.NotNull(protectedText);
       
        AssertSessionPruning(
            databaseMock,
            timeProvider,
            userId);

        // Unprotect using the same dedicated purpose defined in the store
        var unprotectedText = protectedText.Unprotect(
            protector.CreateProtector(RedisTicketStore.SessionPurpose),
            purpose);
        Assert.NotNull(unprotectedText);

        AssertSessionStored(
            databaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                CorrelationId = httpContext.Items[RedisTicketStore.SessionCorrelationKey]!.ToString(),
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionIdHash = unprotectedText.Hash()
            });
    }

    [Fact]
    public void Protect_KickOutMode__ClearsOnlyUnrelatedSessionsAndStoresCurrent()
    {
        // Arrange
        var userId = new Random().Next();
        var authTicket = CreateAuthenticationTicket(userId);

        // Simulate a session with a matching correlation ID already in the store
        var correlationId = Guid.NewGuid().ToString();
        var databaseMock = new Mock<IDatabase>();
        SetupExistingSessions(
            databaseMock,
            correlationId);

        var httpContext = new DefaultHttpContext();
        httpContext.Items[RedisTicketStore.SessionCorrelationKey] = correlationId;
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        var protector = new EphemeralDataProtectionProvider().CreateProtector("Test");
        var securityOptions = CreateSecurityOptions(LoginConcurrencyMode.KickOut);
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var store = new RedisTicketStore(
            databaseMock.Object,
            protector,
            new Mock<IGeoIP2DatabaseReader>().Object,
            httpContextAccessorMock.Object,
            securityOptions!,
            timeProvider,
            Parser.GetDefault());

        var purpose = Guid.NewGuid().ToString();

        // Act
        var protectedText = store.Protect(
            authTicket, 
            purpose);

        // Assert
        Assert.NotNull(protectedText);

        // Deletes only unrelated sessions
        databaseMock.Verify(
          db => db.ScriptEvaluate(
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

        var unprotectedText = protectedText.Unprotect(
            protector.CreateProtector(RedisTicketStore.SessionPurpose), 
            purpose);

        Assert.NotNull(unprotectedText);
        AssertSessionStored(
            databaseMock,
            authTicket,
            new()
            {
                AuthScheme = authTicket.AuthenticationScheme,
                CorrelationId = httpContext.Items[RedisTicketStore.SessionCorrelationKey]!.ToString(),
                DeviceInfo = RedisTicketStore.GenericDeviceInfo,
                LastSeen = timeProvider.GetUtcNow(),
                Location = RedisTicketStore.GenericLocation,
                LoginTime = timeProvider.GetUtcNow(),
                SessionIdHash = unprotectedText.Hash()
            });
    }

    [Fact]
    public async Task GetAll_ReturnsValidResult()
    {
        // Arrange
        var userId = new Random().Next();

        var databaseMock = new Mock<IDatabase>();
        var timeProvider = new FakeTimeProvider();
        var store = CreateStore(
            databaseMock.Object,
            timeProvider);

        SetupExistingSessions(databaseMock);

        // Act
        var result = await store.GetAll(userId);

        // Assert
        // Session pruning
        databaseMock.Verify(
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


        Assert.NotNull(result);
        
        Assert.True(result.Any());
        Assert.True(result.DistinctBy(u => u.CorrelationId).Count() == result.Count());
    }

    [Fact]
    public async Task RetrieveAsync_SessionExists_UpdatesMetadataAndReturnsSession()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var databaseMock = new Mock<IDatabase>();
        var timeProvider = new FakeTimeProvider();
        var store = CreateStore(
            databaseMock.Object,
            timeProvider);

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        var sessionIdHash = sessionId.Hash();
        databaseMock.Setup(db => db.StringGet(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionIdHash}",
            It.IsAny<CommandFlags>()))
            .Returns(TicketSerializer.Default.Serialize(authTicket));

        // Simulate existing user session metadata
        var userSessionDto = new UserSessionDto()
        {
            AuthScheme = Guid.NewGuid().ToString(),
            DeviceInfo = Guid.NewGuid().ToString(),
            Location = Guid.NewGuid().ToString(),
            LoginTime = timeProvider.GetUtcNow().AddDays(-1 * random.Next(short.MaxValue)),
            SessionIdHash = sessionIdHash
        };
        databaseMock.Setup(db => db.HashGet(
            $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}",
            sessionIdHash,
            It.IsAny<CommandFlags>()))
            .Returns(JsonSerializer.Serialize(userSessionDto, _jsonOptions));

        // Act
        var actual = await store.RetrieveAsync(sessionId);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(
            userId.ToString(), 
            authTicket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));

        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.UpdateLastSeenScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"),
                It.Is<RedisValue[]>(
                    v => v[0] == userSessionDto.SessionIdHash
                         && v[1] == timeProvider.GetUtcNow().ToString("O")),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_SessionDoesNotExist_ReturnsNull()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var actual = await store.RetrieveAsync(Guid.NewGuid().ToString());

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

        var httpContext = new DefaultHttpContext();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        // Simulate existing session
        var databaseMock = new Mock<IDatabase>();
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        var sessionIdHash = sessionId.Hash();
        databaseMock.Setup(db => db.StringGet(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionIdHash}",
            It.IsAny<CommandFlags>()))
            .Returns(TicketSerializer.Default.Serialize(authTicket));

        // Simulate existing user session metadata
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var userSessionDto = new UserSessionDto()
        {
            AuthScheme = Guid.NewGuid().ToString(),
            DeviceInfo = Guid.NewGuid().ToString(),
            Location = Guid.NewGuid().ToString(),
            LoginTime = timeProvider.GetUtcNow().AddDays(-1 * random.Next(short.MaxValue)),
            SessionIdHash = sessionIdHash,
            CorrelationId = Guid.NewGuid().ToString(),
        };
        databaseMock.Setup(db => db.HashGet(
            $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}",
            sessionIdHash,
            It.IsAny<CommandFlags>()))
            .Returns(JsonSerializer.Serialize(userSessionDto, _jsonOptions));

        var protector = new EphemeralDataProtectionProvider().CreateProtector("Test");
        var store = new RedisTicketStore(
            databaseMock.Object,
            protector,
            null!,
            httpContextAccessorMock.Object,
            null!,
            timeProvider,
            Parser.GetDefault());

        // Protect using the same dedicated purpose defined in the store
        var purpose = Guid.NewGuid().ToString();
        var protectedText = sessionId.Protect(
            protector.CreateProtector(RedisTicketStore.SessionPurpose), 
            purpose);

        // Act
        var actual = store.Unprotect(
            protectedText, 
            purpose);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(
            userId.ToString(),
            authTicket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));

        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.UpdateLastSeenScriptName)), 
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"),
                 It.Is<RedisValue[]>(
                     v => v[0] == userSessionDto.SessionIdHash
                          && v[1] == timeProvider.GetUtcNow().ToString("O")),
                 It.IsAny<CommandFlags>()),
             Times.Once);

        // Correlation ID is set
        httpContext.Items[RedisTicketStore.SessionCorrelationKey] = userSessionDto.CorrelationId;
    }

    [Fact]
    public async Task RenewAsync_SessionExists_IsSuccessful()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var databaseMock = new Mock<IDatabase>();
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = CreateStore(
            databaseMock.Object,
            timeProvider);

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        var sessionIdHash = sessionId.Hash();
        var userSessionDto = new UserSessionDto()
        {
            AuthScheme = Guid.NewGuid().ToString(),
            DeviceInfo = Guid.NewGuid().ToString(),
            Location = Guid.NewGuid().ToString(),
            LoginTime = timeProvider.GetUtcNow().AddDays(-1 *random.Next(short.MaxValue)),
            SessionIdHash = sessionIdHash
        };
        databaseMock.Setup(db => db.HashGet(
            $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}",
            sessionIdHash,
            It.IsAny<CommandFlags>()))
            .Returns(JsonSerializer.Serialize(userSessionDto, _jsonOptions));

        // Act
        await store.RenewAsync(
            sessionId, 
            authTicket);

        // Assert
        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.RenewAndSyncScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionIdHash}"
                         && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                         && k[2] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"),
                It.Is<RedisValue[]>(
                    v => v[0] == sessionIdHash
                         && v[1] == authTicket.Properties.ExpiresUtc!.Value.ToUnixTimeSeconds()
                         && v[2] == timeProvider.GetUtcNow().ToUnixTimeSeconds()
                         && v[3] == timeProvider.GetUtcNow().ToString("O")
                         && v[4] == TicketSerializer.Default.Serialize(authTicket)),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_IsSuccessful()
    {
        // Arrange
        var userId = new Random().Next();

        var databaseMock = new Mock<IDatabase>();
        var timeProvider = new FakeTimeProvider();
        var store = CreateStore(
            databaseMock.Object,
            timeProvider);

        var sessionId = $"{userId}:{Guid.NewGuid()}";
        var sessionIdHash = sessionId.Hash();

        // Act
        await store.RemoveAsync(sessionId);

        // Assert
        AssertSessionRemoved(
            databaseMock,
            timeProvider,
            userId,
            sessionIdHash);
    }

    [Fact]
    public async Task RemoveProtected_IsSuccessful()
    {
        // Arrange
        var userId = new Random().Next();
        var databaseMock = new Mock<IDatabase>();
        var timeProvider = new FakeTimeProvider();

        var protector = new EphemeralDataProtectionProvider().CreateProtector("Test");
        var store = new RedisTicketStore(
            databaseMock.Object,
            protector,
            null!,
            new Mock<IHttpContextAccessor>().Object,
            null!,
            timeProvider,
            Parser.GetDefault());

        var sessionId = $"{userId}:{Guid.NewGuid()}";

        // Protect using the same dedicated purpose defined in the store
        var purpose = Guid.NewGuid().ToString();
        var protectedText = sessionId.Protect(
            protector.CreateProtector(RedisTicketStore.SessionPurpose),
            purpose);

        // Act
        await store.RemoveProtected(
            protectedText,
            purpose);

        // Assert
        AssertSessionRemoved(
            databaseMock,
            timeProvider,
            userId,
            sessionId.Hash());
    }

    [Fact]
    public async Task Remove_UserIdDoesNotMatchTheOneOnSession_DoesNotRemove()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();
        var authTicket = CreateAuthenticationTicket(userId);

        var databaseMock = new Mock<IDatabase>();
        var store = CreateStore(databaseMock.Object);

        // Simulate existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";
        databaseMock.Setup(db => db.StringGetAsync(
            $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{sessionId.Hash()}",
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TicketSerializer.Default.Serialize(authTicket));

        var wrongUserId = userId + random.Next(1, int.MaxValue);

        // Act
        await store.Remove(    
            wrongUserId,    
            sessionId.Hash());

        // Assert
        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAndSyncScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()),   
            Times.Never);
    }

    [Fact]
    public async Task Remove_SessionDoesNotExist_IsIdempotent()
    {
        // Arrange
        var random = new Random();
        var userId = random.Next();

        var databaseMock = new Mock<IDatabase>();
        var store = CreateStore(databaseMock.Object);

        // Simulate non existing session
        var sessionId = $"{userId}:{Guid.NewGuid()}";

        // Act
        await store.Remove(
            userId,
            sessionId.Hash());

        // Assert
        databaseMock.Verify(
          db => db.ScriptEvaluate(
              It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAndSyncScriptName)),
              It.IsAny<RedisKey[]>(),
              It.IsAny<RedisValue[]>(),
              It.IsAny<CommandFlags>()),
              Times.Never);
    }
    
    [Fact]
    public async Task RemoveAll_IsSuccessful()
    {
        // Arrange
        var userId = new Random().Next();

        var databaseMock = new Mock<IDatabase>();
        var store = CreateStore(databaseMock.Object);

        // Act
        await store.RemoveAll(userId);

        // Assert
        databaseMock.Verify(
           db => db.ScriptEvaluate(
               It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAllScriptName)),
               It.Is<RedisKey[]>(
                   k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                        && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                        && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
               It.Is<RedisValue[]>(v => v.IsNullOrEmpty()),
               It.IsAny<CommandFlags>()),
               Times.Once);
    }

    private static RedisTicketStore CreateStore( 
        IDatabase? database = null,
        TimeProvider? timeProvider = null)
    {
        return new RedisTicketStore(
           database ?? new Mock<IDatabase>().Object,
           new EphemeralDataProtectionProvider().CreateProtector(RedisTicketStore.SessionPurpose),
           new Mock<IGeoIP2DatabaseReader>().Object,
           new Mock<IHttpContextAccessor>().Object,
           new Mock<IOptionsMonitor<SecurityOptions>>().Object,
           timeProvider ?? new FakeTimeProvider(DateTimeOffset.UtcNow),
           Parser.GetDefault());
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
        Mock<IDatabase> databaseMock,
        string correlationId = "test-correlation")
    {
        var existingSessionCount = new Random().Next(1, 10);
        var existingSessions = Enumerable.Range(0, existingSessionCount)
            .Select(i => (RedisValue)JsonSerializer.Serialize(new UserSessionDto
            {
                SessionIdHash = $"user123:session123",
                CorrelationId = correlationId
            },
            _jsonOptions))
            .ToArray();

        databaseMock.Setup(
            db => db.ScriptEvaluate( 
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Returns(RedisResult.Create(existingSessions));

        databaseMock.Setup(
            db => db.ScriptEvaluateAsync(
                It.Is<string>(s => s.Contains(RedisTicketStore.GetAndPruneScriptName)),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(existingSessions));
    }

    private static void AssertSessionPruning(
       Mock<IDatabase> databaseMock,
       TimeProvider timeProvider,
       long userId)
    {
        databaseMock.Verify(
            db => db.ScriptEvaluate(
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
        Mock<IDatabase> databaseMock,
        AuthenticationTicket authenticationTicket,
        UserSessionDto userSessionDto)
    {
        var userId = authenticationTicket.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        databaseMock.Verify(
           db => db.ScriptEvaluate(
               It.Is<string>(s => s.Contains(RedisTicketStore.StoreAndSyncScriptName)),
               It.Is<RedisKey[]>(
                   k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}{userSessionDto.SessionIdHash}"
                        && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                        && k[2] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"),
               It.Is<RedisValue[]>(
                   v => v[0] == userSessionDto.SessionIdHash
                        && v[1] == authenticationTicket.Properties.ExpiresUtc!.Value.ToUnixTimeSeconds()
                        && v[2] == userSessionDto.LastSeen.ToUnixTimeSeconds()
                        && AssertEqual(v[3].ToString(), userSessionDto)
                        && v[4] == TicketSerializer.Default.Serialize(authenticationTicket)),
               It.IsAny<CommandFlags>()),
           Times.Once);
    }

    private static void AssertSessionRemoved(
        Mock<IDatabase> databaseMock,
        TimeProvider timeProvider,
        long userId,
        string sessionIdHash)
    {
        databaseMock.Verify(
            db => db.ScriptEvaluate(
                It.Is<string>(s => s.Contains(RedisTicketStore.RemoveAndSyncScriptName)),
                It.Is<RedisKey[]>(
                    k => k[0] == $"{{User:{userId}}}:{RedisTicketStore.UserSessionPrefix}"
                         && k[1] == $"{{User:{userId}}}:{RedisTicketStore.UserExpiryPrefix}"
                         && k[2] == $"{{User:{userId}}}:{RedisTicketStore.KeyPrefix}"),
                It.Is<RedisValue[]>(
                    v => v[0] == sessionIdHash
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
            && actual.SessionIdHash == expected.SessionIdHash;
    }
}
