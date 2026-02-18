using Domain;
using System.Text.Json.Serialization.Metadata;

namespace Api.Utils;

public static class AuditLogJsonModifier
{
    public static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        foreach (var property in typeInfo.Properties)
        {
            // Ignore large binary data (byte arrays)
            if (property.PropertyType == typeof(byte[]) || property.PropertyType == typeof(Stream))
            {
                property.Get = _ => Domain.Constants.BinaryDataMask;
                continue;
            }

            // Mask sensitive fields and fields marked as not audited
            var hasAttribute = property.AttributeProvider?.IsDefined(typeof(NotAuditedAttribute), true) ?? false;
            var isSensitive = Domain.Constants.SensitiveFieldNames.Contains(property.Name);
            if (hasAttribute || isSensitive)
            {
                property.Get = _ => Domain.Constants.Mask;
            }

            // Trim long strings
            if (property.PropertyType == typeof(string))
            {
                var originalGetter = property.Get;
                if (originalGetter != null)
                {
                    property.Get = obj =>
                    {
                        var value = originalGetter(obj) as string;
                        if (value?.Length > Domain.Constants.StringMaxLength)
                        {
                            return string.Concat(
                                value.AsSpan(0, Domain.Constants.StringMaxLength - Domain.Constants.TruncationIndicator.Length), 
                                Domain.Constants.TruncationIndicator);
                        }
                        return value;
                    };
                }
            }
        }
    }
}


