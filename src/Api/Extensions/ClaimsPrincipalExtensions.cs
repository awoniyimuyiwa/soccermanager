using System.Security.Claims;

namespace Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal user)
        => long.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException());
}
