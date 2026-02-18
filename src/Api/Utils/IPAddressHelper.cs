using System.Net;
using System.Net.Sockets;

namespace Api.Utils;

public static class IPAddressHelper
{
    /// <summary>
    /// Checks if an IP address falls within a specified CIDR range. 
    /// Both IPv4 and IPv6 are supported by normalizing all inputs to IPv6.
    /// </summary>
    /// <param name="ipAddress">The IP address to validate.</param>
    /// <param name="cidr">The CIDR range (e.g., "192.168.1.0/24" or "::ffff:192.168.1.0/120").</param>
    /// <returns>
    /// True if the address is within the range; false if it is not, 
    /// or if the <paramref name="ipAddress"/> is null or <paramref name="cidr"/> is malformed.
    /// </returns>
    /// <remarks>
    /// This method normalizes addresses to IPv6 using <see cref="IPAddress.MapToIPv6"/>. 
    /// Because every IPv4 address has a mathematically defined "home" in IPv6 ([RFC 4291](https://datatracker.ietf.org)), 
    /// but the reverse is not true, this ensures consistent comparison logic for 
    /// both legacy local networks and modern IPv6-only environments.
    /// </remarks>
    public static bool IsInCidrRange(
        IPAddress ipAddress,
        string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr)) { return false; }

        var cidrParts = cidr.Split('/');
        if (cidrParts.Length != 2) { return false; }

        if (!IPAddress.TryParse(cidrParts[0], out var networkAddress)) { return false; }

        if (!int.TryParse(cidrParts[1], out var prefixLength)) { return false; }

        // Normalize both to IPv6 to ensure consistent byte lengths
        var addressBytes = ipAddress.MapToIPv6().GetAddressBytes();
        var networkBytes = networkAddress.MapToIPv6().GetAddressBytes();

        // If the network address was IPv4 (prefix 0-32), 
        // mapping to IPv6 adds 96 bits of padding (::ffff:0:0/96).
        // Offset the prefixLength for the comparison to work.
        var effectivePrefix = networkAddress.AddressFamily == AddressFamily.InterNetwork
            ? prefixLength + 96
            : prefixLength;

        var fullBytes = effectivePrefix / 8;
        var remainingBits = effectivePrefix % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (remainingBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));

            if ((addressBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
            {
                return false;
            }
        }

        return true;
    }
}


