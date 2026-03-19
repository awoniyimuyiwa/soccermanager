using Api.Extensions;
using Domain;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization.Metadata;

namespace Api.Utils;

public static class SecurityJsonModifier
{
    public static void Modify(JsonTypeInfo typeInfo, IDataProtector protector)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType != typeof(string)) continue;

            var originalGetter = property.Get;

            if (originalGetter == null) continue;

            var attributes = property.AttributeProvider;

            if (attributes == null) continue;

            if (attributes.IsDefined(typeof(ProtectedAttribute), true))
            {
                var purpose = EncryptionUtils.GetPurpose(typeInfo.Type, property.Name);

                property.Get = obj =>
                {
                    var val = originalGetter(obj) as string;

                    if (string.IsNullOrWhiteSpace(val)) return null;

                    return val.Protect(protector, purpose);
                };
            }
            else if (attributes.IsDefined(typeof(MaskedAttribute), true))
            {
                /*
                 * A single static purpose is used here because [Masked] properties are
                 * purely for UI redaction (preventing sensitive values from being exposed
                 * in plain text). Unlike Cursors, these values are not meant to be
                 * round-tripped or used as functional input elsewhere in the API,
                 * so resource-level cryptographic isolation is unnecessary.
                 */
                property.Get = obj =>
                {
                    var protectedText = originalGetter(obj) as string;

                    return protectedText.Mask(protector, Constants.SecretProtectorPurpose);
                };
            }
        }
    }
}

