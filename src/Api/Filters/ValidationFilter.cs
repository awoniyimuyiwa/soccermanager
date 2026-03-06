using System.ComponentModel.DataAnnotations;

namespace Api.Filters;

/// <summary>
/// A generic validation filter for Minimal APIs that executes Data Annotation checks before the handler.
/// </summary>
/// <typeparam name="T">The type of the request body or parameter to validate.</typeparam>
public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Ensure we find the actual object passed to the endpoint
        var input = context.Arguments.FirstOrDefault(x => x is T) is T value ? value : default;

        if (input is not null)
        {
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(input, new ValidationContext(input), validationResults, true))
            {
                return TypedResults.ValidationProblem(validationResults
                    .GroupBy(x => x.MemberNames.FirstOrDefault() ?? string.Empty)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.ErrorMessage ?? "Invalid").ToArray()
                    ));
            }
        }

        return await next(context);
    }
}
