using Api.Options;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using System.Security.Cryptography.X509Certificates;

namespace Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures .NET Data Protection to persist keys in Redis and encrypt them using a certificate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Verification (Redis CLI):</b><br />
    /// To verify that .NET Data Protection keys are successfully stored and encrypted in Redis:
    /// </para>
    /// <para>
    /// 1. Connect: <code>redis-cli</code> (Add -h [host] -p [port] -a [password] for remote/cloud)
    /// </para>
    /// <para>
    /// 2. Locate: <code>KEYS *DataProtection*</code>
    /// </para>
    /// <para>
    /// 3. Inspect: <code>LRANGE "DataProtection-Keys" 0 -1</code>
    /// </para>
    /// <para>
    /// 4. Confirm Encryption:<br />
    /// [SUCCESS]: XML contains an &lt;encryptedSecret&gt; tag with a long Base64 string.<br />
    /// [RISK]: If &lt;masterKey&gt; is in plaintext, certificate protection is NOT active.
    /// </para>
    /// <para>
    /// <b>Certificate Rotation:</b><br />
    /// To avoid invalidating user sessions during rotation, do NOT swap the primary certificate immediately. 
    /// Follow the 3-step <b>Zero-Downtime Rotation Workflow</b> documented in the <b>README.md</b>.
    /// </para>
    /// <para>
    /// <b>Why this is necessary:</b><br />
    /// Simply replacing a certificate makes existing Redis keys unreadable instantly, 
    /// causing all current user sessions (cookies) and protected data to be invalidated.
    /// </para>
    /// <para>
    /// <b>Azure Key Vault Automation:</b><br />
    /// Using [Azure Key Vault](https://learn.microsoft.com) automates this process via versionless key URIs:
    /// <br />• <b>Key Identification:</b> AKV embeds the version ID in Redis metadata.
    /// <br />• <b>Automatic Decryption:</b> Nodes request specific versions from AKV for decryption.
    /// <br />• <b>No "Split-Brain":</b> AKV handles versioning transparently, allowing nodes with different configurations to decrypt each other's keys.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddCustomDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IConnectionMultiplexer connectionMultiplexer,
        ILogger logger,
        TimeProvider? timeProvider = null)
    {
        var customDataProtectionOptions = 
            configuration.GetRequiredSection(CustomDataProtectionOptions.SectionName)           
            .Get<CustomDataProtectionOptions>();

        if (!Enum.TryParse<X509KeyStorageFlags>(
            customDataProtectionOptions!.StorageFlag,
            ignoreCase: true,
            out var certStorageFlag))
        {
            if (!string.IsNullOrWhiteSpace(customDataProtectionOptions!.StorageFlag) 
                && logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Invalid StorageFlag '{Flag}' in config. Falling back to EphemeralKeySet.", 
                    customDataProtectionOptions.StorageFlag);
            }
            certStorageFlag = X509KeyStorageFlags.EphemeralKeySet;
        }

        if (customDataProtectionOptions.Certificates.Count == 0)
            throw new InvalidOperationException("No DataProtection certificates configured.");

        var certs = customDataProtectionOptions.Certificates
            .Select(c => X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(c.Base64),
                c.Password,
                certStorageFlag))
            .ToArray();

        foreach (var cert in certs)
        {
            if (!cert.HasPrivateKey)
            {
                throw new InvalidOperationException($"Certificate with thumbprint {cert.Thumbprint} does not have a private key. Data Protection requires certificates with private keys for encryption.");
            }
            
            ValidateCertificateExpiration(
                cert, 
                logger,
                timeProvider ??= TimeProvider.System);

            // Let the DI container manage the certificate's lifetime
            // This makes the 'cert' instance available to the entire app
            // and it can be injected into any other part of the app (like a custom signing service)
            // using IEnumerable<X509Certificate2>
            services.AddSingleton(cert);         
        }

        var primaryCert = certs.First();

        services.AddDataProtection()
            .SetApplicationName(customDataProtectionOptions.ApplicationName)
            .PersistKeysToStackExchangeRedis(connectionMultiplexer, "DataProtection-Keys")
            // The first cert in the list is the only one used for new encryption
            .ProtectKeysWithCertificate(primaryCert)
            // Allow decryption using any of the certificates in the list (Primary + Backups)
            .UnprotectKeysWithAnyCertificate(certs);

        return services;
    }

    /// <summary>
    /// By default, Data Protection generates a new master key every 90 days.
    /// When it attempts to "roll" the key: It tries to encrypt the new key using the certificate.
    /// If the certificate is expired, the encryption process will throw an exception during the key generation phase.
    /// Unlike web browsers, Data Protection doesn't usually perform a full revocation check or chain validation when encrypting;
    /// it primarily cares that it has a valid X509Certificate2 object with a private key.
    /// However, if the OS marks the certificate as "Invalid," the .ProtectKeysWithCertificate() call will fail.
    /// 
    /// Data Protection allows keys to "expire" (default 90 days), but it never deletes them. 
    /// It just creates a new one for new encryption. As long as the old XML keys remain in the Redis list, 
    /// your IDataProtector can still decrypt data created 5 years ago.
    /// </summary>
    private static void ValidateCertificateExpiration(
        X509Certificate2 cert,
        ILogger logger,
        TimeProvider timeProvider)
    {
        // Certificates are stored in UTC, so we convert NotAfter to UTC for a valid comparison
        DateTime expirationUtc = cert.NotAfter.ToUniversalTime();
        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (expirationUtc < nowUtc)
        {
            if (logger.IsEnabled(LogLevel.Critical))
            {
                logger.LogCritical(
                    "❌ DATA PROTECTION CERTIFICATE EXPIRED! Thumbprint: {Thumbprint}. Expiration: {ExpireDate} UTC. New keys CANNOT be generated.",
                    cert.Thumbprint,
                    expirationUtc);
            }
            
        }
        else if (expirationUtc < nowUtc.AddDays(30))
        {
            var daysRemaining = (expirationUtc - nowUtc).Days;
            if (logger.IsEnabled (LogLevel.Warning))
            {
                logger.LogWarning(
                    "⚠️ DATA PROTECTION CERTIFICATE EXPIRES SOON! Thumbprint: {Thumbprint}. Expires in {Days} days ({ExpireDate} UTC). Prepare for rotation.",
                    cert.Thumbprint,
                    daysRemaining,
                    expirationUtc);
            }
        }
        else
        {
            if (logger.IsEnabled (LogLevel.Information))
            {
                logger.LogInformation(
                    "✅ Data Protection Certificate is valid until {ExpireDate} UTC.",
                    expirationUtc);
            }
        }
    }
}
    
