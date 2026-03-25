using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

public class AntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IEndpointFilter
{
    // MVC Controller Path
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // If validation fails (false), short-circuit via MVC Result
        if (!await IsValid(context.HttpContext))
        {
            context.Result = new BadRequestObjectResult(Constants.AntiforgeryValidationErrorMessage);
        }
    }

    // Minimal API Path
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, 
        EndpointFilterDelegate next)
    {
        // If validation fails (false), return Minimal API Result
        if (!await IsValid(context.HttpContext))
        {
            return TypedResults.BadRequest(Constants.AntiforgeryValidationErrorMessage);
        }

        return await next(context);
    }

    private async Task<bool> IsValid(HttpContext httpContext)
    {
        if (ShouldSkipAntiforgeryValidation(httpContext))
        {
            return true;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool ShouldSkipAntiforgeryValidation(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var antiforgeryMetadata = endpoint?.Metadata.GetMetadata<IAntiforgeryMetadata>();
        var authHeader = httpContext.Request.Headers.Authorization.ToString();

        var method = httpContext.Request.Method;
        if (antiforgeryMetadata?.RequiresValidation == false
            || HttpMethods.IsGet(method)
            || HttpMethods.IsHead(method)
            || HttpMethods.IsOptions(method)
            || HttpMethods.IsTrace(method)
            || authHeader.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}