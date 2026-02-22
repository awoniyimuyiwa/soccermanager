using Api.Options;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Api.UnitTests;

public class RateLimitServiceTests
{
    [Fact]
    public void IsBypassed_ExactWhitelistMatch_ReturnsTrue()
    {
        var options = new RateLimitOptions
        {
            WhiteList = ["auth-user", "127.0.0.1"]
        };

        var rateLimitService = CreateServiceWithOptions(options);

        Assert.True(rateLimitService.IsBypassed("auth-user"));
        Assert.True(rateLimitService.IsBypassed("127.0.0.1"));
    }

    [Fact]
    public void IsBypassed_NonMatchingIpOrString_ReturnsFalse()
    {
        var options = new RateLimitOptions
        {
            WhiteList = ["10.0.0.0/8", "allowed-user"]
        };

        var rateLimitService = CreateServiceWithOptions(options);

        // Not in whitelist as a string
        Assert.False(rateLimitService.IsBypassed("some-random-user"));

        // IP not in the CIDR range
        Assert.False(rateLimitService.IsBypassed("192.168.1.1"));
    }

    [Theory]
    // Inside (First usable)
    [InlineData("192.168.1.1", "192.168.1.0/29", true)]

    // Inside (Last usable/Broadcast)
    [InlineData("192.168.1.7", "192.168.1.0/29", true)]

    // OUTSIDE (One digit off)
    [InlineData("192.168.1.8", "192.168.1.0/29", false)]

    // IPv6 Match
    [InlineData("2001:db8::1", "2001:db8::/32", true)]

    // Standard C-Class
    [InlineData("1.1.1.1", "1.1.1.0/24", true)]

    // Connection is via Mapped IPv6, whitelist contains IPv4
    [InlineData("::ffff:192.168.1.1", "192.168.1.0/24", true)]

    // Connection is via IPv4, whitelist contains Mapped IPv6
    // Note:
    //   .NET converts 192.168.1.1 into ::ffff:192.168.1.1.
    //   In IPv6, the "IPv4-mapped" space starts at the 96th bit.
    //
    //   If the whitelist contains an IPv4 /24, the helper logic adds 96 bits,
    //   resulting in a /120 in IPv6 space. The bitmask then checks bits 96 
    //   through 120, perfectly isolating the 192.168.1 portion.
    //
    //   Why /120?
    //   In the IPv6 Addressing Architecture, the mapping looks like this:
    //     Bits 000-079: All zeros (0000...0000)
    //     Bits 080-095: All ones  (ffff)
    //     Bits 096-127: The original 32-bit IPv4 address
    //
    //   Therefore, an IPv4 /24 maps exactly to bits 96 through 120 of the IPv6 address.
    [InlineData("192.168.1.1", "::ffff:192.168.1.0/120", true)]

    // Real IPv6 vs IPv4 (Should NEVER match)
    [InlineData("2001:db8::1", "192.168.1.0/24", false)]

    [InlineData("192.168.1.1", "", false)]

    [InlineData("192.168.1.1", "invalid/format", false)]

    public void IsBypassed_IPInCidrRange_ShouldReturnValidResult(
        string ip,
        string whiteList,
        bool expected)
    {
        var options = new RateLimitOptions
        {
            WhiteList = [whiteList]
        };

        var rateLimitService = CreateServiceWithOptions(options);

        Assert.Equal(expected, rateLimitService.IsBypassed(ip));
    }

    static RateLimitService CreateServiceWithOptions(RateLimitOptions options)
    {
        var optionsMock = new Mock<IOptions<RateLimitOptions>>();
        optionsMock.Setup(o => o.Value).Returns(options);

        var connectionMock = new Mock<IConnectionMultiplexer>();
        connectionMock.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(Mock.Of<IDatabase>());    

        return new RateLimitService(
            connectionMock.Object,
            optionsMock.Object);
    }
}