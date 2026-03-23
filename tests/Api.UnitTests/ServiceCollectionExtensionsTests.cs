using Api.Extensions;
using Api.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Api.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCustomDataProtection_ShouldLogCritical_WhenCertIsExpired()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var password = "TestPassword123";
        var cert = GenerateCertificate(password, now.AddDays(-1));
        var (services,
            customDataProtectionOptions,
            logger) = GetMocks(
            password,
            cert);
        var timeProvider = new FakeTimeProvider(now);

        // Act
        services.AddCustomDataProtection(
            "",
            customDataProtectionOptions,
            logger,
            timeProvider);

        // Assert
        VerifyLog(
            logger,
            LogLevel.Critical, 
            "EXPIRED");
    }

    [Fact]
    public void AddCustomDataProtection_ShouldLogWarning_WhenCertExpiresSoon()
    {
        // Arrange: Cert expires in 15 days
        var now = DateTimeOffset.UtcNow;
        var password = "TestPassword123";
        var cert = GenerateCertificate(password,now.AddDays(15));
        var (services,
           customDataProtectionOptions,
           logger) = GetMocks(
           password,
           cert);
        var timeProvider = new FakeTimeProvider(now);

        // Act
        services.AddCustomDataProtection(
            "",
            customDataProtectionOptions,
            logger,
            timeProvider);

        // Assert
        VerifyLog(
            logger, 
            LogLevel.Warning, 
            "EXPIRES SOON");
    }

    [Fact]
    public void AddCustomDataProtection_ShouldLogInformation_WhenCertIsHealthy()
    {
        // Arrange: Cert expires in 365 days
        var now = DateTimeOffset.UtcNow;
        var password = "TestPassword123";
        var cert = GenerateCertificate(password, now.AddDays(365));
        var (services,
            customDataProtectionOptions,
            logger) = GetMocks(
            password,
            cert);
        var timeProvider = new FakeTimeProvider(now);

        // Act
        services.AddCustomDataProtection(
            "",
            customDataProtectionOptions,
            logger,
            timeProvider);

        // Assert
        VerifyLog(
            logger,
            LogLevel.Information, 
            "is valid until");
    }

    public static string GenerateCertificate(
        string password,
        DateTimeOffset expiresAt)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=TestCert",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            false,
            false,
            0,
            false));

        var cert = request.CreateSelfSigned(
            expiresAt.AddDays(-1),
            expiresAt);

        var pfxBytes = cert.Export(X509ContentType.Pkcs12, password);

        return Convert.ToBase64String(pfxBytes);
    }

    private static (
       ServiceCollection services,
       CustomDataProtectionOptions customDataProtectionOptions,
       ILogger logger) GetMocks(
       string testPassword,
       string expiredCertBase64)
    {
        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var services = new ServiceCollection();

        var customDataProtectionOptions = new CustomDataProtectionOptions
        {
            Certificates =
            [
                new() {
                    Base64 = expiredCertBase64,
                    Password = testPassword
                }
            ]
        };

        return (
            services,
            customDataProtectionOptions,
            loggerMock.Object);
    }

    private static void VerifyLog(
        ILogger logger,
        LogLevel level,
        string messagePart)
    {
        Mock.Get(logger).Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messagePart)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
