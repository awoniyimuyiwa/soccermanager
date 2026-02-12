using System.ComponentModel.DataAnnotations;

namespace Api.Attributes;

public class AgeRangeAttribute(
    int min, 
    int max) : ValidationAttribute
{

    readonly int _min = min;
    readonly int _max = max;

    protected override ValidationResult IsValid(
        object? value, 
        ValidationContext validationContext)
    {
        var timeProvider = validationContext.GetService<TimeProvider>() 
            ?? throw new InvalidOperationException($"{nameof(TimeProvider)} cannot be null");

        if (value is DateOnly dateOfBirth)
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().Date);
            var age = today.Year - dateOfBirth.Year;

            // Adjust age if the birthday hasn't occurred this year yet
            if (dateOfBirth > today.AddYears(-age))
            {
                age--;
            }

            if (age >= _min && age <= _max)
            {
                return ValidationResult.Success!;
            }
            else
            {
                var errorMessage = $"Must be between {_min} and {_max} years old.";

                return new ValidationResult(errorMessage);
            }
        }

        // If the value is not a DateOnly, or is null (can be handled by [Required] separately)
        return new ValidationResult("Invalid date format or value.");
    }
}



