using Api.Extensions;
using Domain;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json.Serialization.Metadata;

namespace Api.Utils;

public static class MaskedJsonModifier
{
    public static void Modify(JsonTypeInfo typeInfo, IDataProtector protector)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        foreach (var property in typeInfo.Properties)
        {
            // Check for the [Masked] attribute
            var isMasked = property.AttributeProvider?
                .IsDefined(typeof(MaskedAttribute), true) ?? false;

            if (isMasked && property.PropertyType == typeof(string))
            {
                var originalGetter = property.Get;
                if (originalGetter != null)
                {
                    // Wrap the getter to mask the value on-the-fly
                    property.Get = obj =>
                    {
                        var protectedText = originalGetter(obj) as string;

                        return protectedText.Mask(
                            protector, 
                            Constants.SecretProtectorPurpose);
                    };
                }
            }
        }
    }
}

