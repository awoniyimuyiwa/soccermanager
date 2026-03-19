using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Api.Attributes;

public sealed class UniqueMaxAttribute(int maxLength) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If the value is null or not a collection, we skip (handled by [Required] if needed)
        if (value is not IEnumerable enumerable)
            return ValidationResult.Success;

        // Cast to object list once to avoid multiple enumerations
        var list = enumerable.Cast<object>().ToList();

        if (list.Count == 0)
            return ValidationResult.Success;

        if (list.Count > maxLength)
            return new ValidationResult($"{validationContext.DisplayName} cannot exceed {maxLength} items.");

        // Check for uniqueness
        if (list.Distinct().Count() != list.Count)
            return new ValidationResult($"{validationContext.DisplayName} must contain unique items.");

        return ValidationResult.Success;
    }
}
