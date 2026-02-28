using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Api.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Generates a secure, non-reversible hash of the provided string to prevent plain-text exposure in storage.
    /// </summary>
    /// <param name="key">The sensitive string (e.g., a Session ID) to be hashed.</param>
    /// <returns>A URL-safe Base64 encoded SHA256 hash.</returns>
    /// <remarks>
    /// Use this to protect sensitive identifiers before persisting them to external stores like Redis.
    /// The URL-safe format ensures compatibility with web protocols and simplifies administrative lookups.
    /// </remarks>
    public static string Hash(this string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        // Base64Url (- for +, _ for /, no padding(=))
        return Base64Url.EncodeToString(bytes);
    }
}