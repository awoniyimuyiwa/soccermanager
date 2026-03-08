using Api.Filters;

namespace Api.Extensions;

public static class ValidationExtensions
{
    /// <summary>
    /// Adds a validation filter to the endpoint that checks Data Annotations.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<ValidationFilter<T>>()         
            .ProducesValidationProblem(); // Adds 400 response to Swagger
    }
}
