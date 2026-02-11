using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Api.Attributes;

public sealed class CountryCodeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string countryCode || string.IsNullOrWhiteSpace(countryCode))
        {
            return ValidationResult.Success;
        }

        try
        {
            // Validate if the string matches a known ISO 3166 code
            var region = new RegionInfo(countryCode);
            return ValidationResult.Success;
        }
        catch (ArgumentException)
        {
            return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} is not a valid ISO country code.");
        }
    }
}



