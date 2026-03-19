namespace Api.Utils;

public static class EncryptionUtils
{
    public static string GetPurpose(Type type, string propertyName)
    {
        // Use FullName to ensure 'MyApp.Models.User' != 'External.User'
        // If FullName is null (e.g. for some dynamic types), fallback to Name
        var typeIdentity = type.FullName ?? type.Name;

        // Force lowercase to ensure 'Next' (Property) and 'next' (Parameter) always match
        return $"Security.{typeIdentity}.{propertyName.ToLowerInvariant()}";
    }
}