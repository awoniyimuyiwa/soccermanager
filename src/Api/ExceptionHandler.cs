using Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api;

public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    readonly ILogger<ExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception has occurred: {ErrorMessage}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,

            Title = "An error occurred while processing your request.",

            Detail = "Internal Server Error",

            Instance = httpContext.Request.Path
        };

        // You can map specific exceptions to different status codes here
        if (exception is ConcurrencyException) 
        { 
            problemDetails.Status = StatusCodes.Status409Conflict; 
            problemDetails.Title = "A concurrency conflict occurred.";
            problemDetails.Detail = "Please refresh and try again.";
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Return true to indicate that the exception has been handled and stop propagation

        return true;
    }
}
