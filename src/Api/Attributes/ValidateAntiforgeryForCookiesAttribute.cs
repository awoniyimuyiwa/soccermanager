using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Attributes;

[AttributeUsage(AttributeTargets.All)]
public class ValidateAntiforgeryForCookiesAttribute : Attribute, IFilterFactory, IFilterMetadata, IOrderedFilter
{
    public bool IsReusable => false;

    //
    // Summary:
    //     Gets the order value for determining the order of execution of filters. Filters
    //     execute in ascending numeric value of the Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute.Order
    //     property.
    //
    // Remarks:
    //     Filters are executed in an ordering determined by an ascending sort of the Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute.Order
    //     property.
    //
    //     The default Order for this attribute is 1000 because it must run after any filter
    //     which does authentication or login in order to allow them to behave as expected
    //     (ie Unauthenticated or Redirect instead of 400).
    //
    //     Look at Microsoft.AspNetCore.Mvc.Filters.IOrderedFilter.Order for more detailed
    //     info.
    public int Order { get; set; } = 1000;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new ValidateAntiforgeryFilter(serviceProvider.GetRequiredService<IAntiforgery>());
    }

    private class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
    {
        readonly IAntiforgery _antiforgery = antiforgery;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;

            // Skip if the identity was created by the Bearer scheme
            // Check the "AuthenticationMethod" or specific scheme name used
            var authScheme = context.HttpContext.Items["__AspNetCore_Authentication_Scheme"] as string;

            // Alternatively, check the claims or the primary identity's label
            if (context.HttpContext.Request.Headers.ContainsKey("Authorization")
                && context.HttpContext.Request.Headers.Authorization.ToString().StartsWith("Bearer "))
            {
                return;
            }

            // Perform validation for Cookies
            try
            {
                await _antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new BadRequestResult();
            }
        }
    }
}
