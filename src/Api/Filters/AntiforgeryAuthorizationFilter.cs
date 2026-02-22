using Api.Extensions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
/// Provides an authorization filter that validates anti-forgery tokens for incoming requests to controler endpoints, helping to protect
/// ASP.NET Core applications from Cross-Site Request Forgery (CSRF) attacks.
/// </summary>
/// <remarks>This filter automatically skips validation for safe HTTP methods (GET, HEAD, OPTIONS, TRACE) and for
/// requests authenticated with Bearer tokens, as these scenarios are not susceptible to CSRF. 
/// Validation is performed for requests using cookie-based authentication, 
/// and a bad request result is returned if the anti-forgery token is
/// invalid.
/// </remarks>
/// <param name="antiforgery">The anti-forgery service used to validate tokens in HTTP requests.</param>
public class AntiforgeryAuthorizationFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.ShouldSkipAntiforgeryValidation())
        {
            return;
        }

        // Validate when cookie is used for authentication
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(Constants.AntiforgeryValidationErrorMesage);
        }
    }
}