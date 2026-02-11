using Api.Attributes;
using Domain;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;

namespace Api.MiddleWares;

public class AuditLogMiddleware(RequestDelegate next)
{
    readonly RequestDelegate _next = next;

    public async Task InvokeAsync( 
        HttpContext context,
        IAuditLogManager auditLogManager,
        ILogger<AuditLogMiddleware> logger,
        TimeProvider timeProvider)
    {
        if (!ShouldAudit(context))
        {
            await _next(context);
            return;
        }

        var startTime = timeProvider.GetTimestamp();
        using var auditScope = auditLogManager.BeginScope();

        try
        {
            await _next(context);
            auditLogManager.Current!.StatusCode = context.Response.StatusCode;
        }
        catch (Exception ex)
        {
            var exception = ex.ToString();
            auditLogManager.Current!.Exception = exception.Length > Domain.Constants.MaxAuditLogStringLength
                ? string.Concat(
                    exception.AsSpan(0, Domain.Constants.MaxAuditLogStringLength - Domain.Constants.TruncationIndicator.Length), 
                    Domain.Constants.TruncationIndicator)
                : exception;
            auditLogManager.Current!.StatusCode = (int)HttpStatusCode.InternalServerError;
            throw;
        }
        finally
        {
            try
            {
                auditLogManager.Current!.BrowserInfo = context.Request.Headers.UserAgent;
                auditLogManager.Current!.Duration = timeProvider.GetElapsedTime(startTime).TotalMilliseconds;
                auditLogManager.Current!.HttpMethod = context.Request.Method;
                auditLogManager.Current!.IpAddress = MaskIpAddress(context.Connection?.RemoteIpAddress?.ToString());
                auditLogManager.Current!.RequestId = context.TraceIdentifier;
                auditLogManager.Current!.Url = context.Request.Path;
                auditLogManager.Current!.UserId = long.TryParse(context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null;

                await auditLogManager.SaveCurrent();
            }
            catch (Exception auditEx)
            {
                logger.LogCritical(auditEx, "Audit log couldn't be saved");
            }
        }
    }

    private static bool ShouldAudit(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        // If neessary, adjust condition to include other endpoints that AuditedAttribute isn't added to
        return endpoint?.Metadata.GetMetadata<AuditedAttribute>() != null;
    }

    /// <summary>
    /// Mask IP for GDPR comliance, to protect personally identifiable information (PII) per Google/Dynatrace standard.
    /// The last octet of IPv4 addresses and the last 80 bits of IPv6 addresses are replaced with zeros.
    /// If needed, geolocation lookups(city-level closest) can be done using the anonymized IP addresses and rounded GPS coordinates(~10 km).
    /// </summary>
    /// <param name="ip"></param>
    /// <returns>Masked ip</returns>
    private static string? MaskIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return ip;

        if (IPAddress.TryParse(ip, out var address))
        {
            var bytes = address.GetAddressBytes();

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                // Zero out the last octet (e.g., 192.168.1.100 -> 192.168.1.0)
                bytes[3] = 0;
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Zero out the last 80 bits (10 bytes)
                for (int i = 6; i < bytes.Length; i++) bytes[i] = 0;
            }

            return new IPAddress(bytes).ToString();
        }

        return ip;
    }
}
