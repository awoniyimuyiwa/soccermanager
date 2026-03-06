using Microsoft.AspNetCore.DataProtection;
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

    /// <summary>
    /// Cryptographically protects the string and returns a URL-safe encoded result.
    /// </summary>
    /// <param name="unprotectedText">The raw string to protect (e.g., a SessionId).</param>
    /// <param name="protector">The base data protector.</param>
    /// <param name="purpose">An optional specific purpose string for additional cryptographic isolation.</param>
    /// <returns>An encrypted, URL-safe Base64 string.</returns>
    public static string Protect(
         this string unprotectedText,
         IDataProtector protector,
         string? purpose)
    {
        // Apply purpose-based isolation if provided
        var purposeProtector = string.IsNullOrWhiteSpace(purpose)
            ? protector
            : protector.CreateProtector(purpose);

        // Convert string to bytes for the protector
        var unprotectedBytes = Encoding.UTF8.GetBytes(unprotectedText);

        // Encrypt the data
        var protectedBytes = purposeProtector.Protect(unprotectedBytes);

        // Encode to a URL-safe format (removes +, /, and =) for HTTP transport
        return Base64Url.EncodeToString(protectedBytes);
    }

    /// <summary>
    /// Decodes and decrypts a URL-safe protected string back to its original value.
    /// </summary>
    /// <param name="protectedText">The encrypted, Base64Url encoded string.</param>
    /// <param name="protector">
    /// The <see cref="IDataProtector"/> used for cryptographic operations. 
    /// Note: The purpose chain of this instance must exactly match the one used during protection.
    /// </param>
    /// <param name="purpose">The optional runtime purpose provided by the framework.</param>
    /// <returns>The original raw string, or null if decryption or decoding fails.</returns>
    public static string? Unprotect(
         this string protectedText,
         IDataProtector protector,
         string? purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return null;

        try
        {
            // Decode from URL-safe Base64
            var protectedBytes = Base64Url.DecodeFromChars(protectedText);
            if (protectedBytes == null) return null;

            // Select the correct protector scope
            var purposeProtector = string.IsNullOrWhiteSpace(purpose)
                ? protector
                : protector.CreateProtector(purpose);

            // Decrypt the bytes
            var unprotectedBytes = purposeProtector.Unprotect(protectedBytes);

            // Convert back to the original string
            return Encoding.UTF8.GetString(unprotectedBytes);
        }
        catch (Exception)
        {
            // Return null on tampering, expiry, or malformed input
            return null;
        }
    }

    /// <summary>
    /// Decrypts the protected text to create a masked version (e.g., ••••1234) for UI display.
    /// </summary>
    /// <param name="protectedText">The encrypted string retrieved from the database.</param>
    /// <param name="protector">The <see cref="IDataProtector"/> used to decrypt the text.</param>
    /// <param name="purpose">The specific purpose string used when the text was originally encrypted.</param>
    /// <returns>A masked string if decryption succeeds; otherwise, a generic mask or null.</returns>
    public static string? Mask(
        this string? protectedText,
        IDataProtector protector,
        string? purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return null;

        const string mask = "••••";

        try
        {
            var unProtectedText = protectedText.Unprotect(protector, purpose);

            if (string.IsNullOrWhiteSpace(unProtectedText)
                || unProtectedText.Length <= 4)
            {
                return mask;
            }

            // Returns the mask followed by the last 4 characters
            return $"{mask}{unProtectedText[^4..]}";
        }
        catch
        {
            // Fallback if the key cannot be decrypted (e.g., after a master key rotation)
            return mask;
        }
    }
}