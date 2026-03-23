using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Api.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class UniqueMaxAttribute(int maxLength = Constants.MaxLengthOfList) : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value, 
        ValidationContext validationContext)
    {
        if (value is not IEnumerable enumerable)
            return ValidationResult.Success;

        var list = enumerable.Cast<object>().ToList();
        if (list.Count == 0)
            return ValidationResult.Success;

        // Try to get type from the Property Info, fallback to the runtime type of the value
        var memberType = validationContext.ObjectType
            .GetProperty(validationContext.MemberName ?? string.Empty)?
            .PropertyType ?? value.GetType();

        // Handle both Arrays (T[]) and Collections (List<T>, IReadOnlyCollection<T>)
        Type? elementType = null;
        if (memberType.IsArray)
        {
            elementType = memberType.GetElementType();
        }
        else if (memberType.IsGenericType)
        {
            elementType = memberType.GetGenericArguments().FirstOrDefault();
        }

        int effectiveMax = (elementType is { IsEnum: true })
            ? Enum.GetValues(elementType).Length
            : maxLength;

        if (list.Count > effectiveMax)
            return new ValidationResult($"{validationContext.DisplayName} cannot exceed {effectiveMax} items.");

        if (list.Distinct().Count() != list.Count)
            return new ValidationResult($"{validationContext.DisplayName} must contain unique items.");

        return ValidationResult.Success;
    }
}
